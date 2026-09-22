using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using AutomatedClashRunner.Services.Interfaces;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;

namespace AutomatedClashRunner.Services
{
    /// <summary>
    /// Thread-safe native interop helper to extract and copy redline markups (ellipses, clouds,
    /// text annotations, lines, arrows, tags) and review comments from Navisworks ClashResult
    /// and ClashResultGroup instances into exported SavedViewpoint instances.
    /// </summary>
    public static class NativeClashRedlineHelper
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetRedlinesDelegate(IntPtr issuePtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr AssignRedlinesDelegate(IntPtr destList, IntPtr srcList);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MergeRedlinesDelegate(IntPtr destList, IntPtr srcList);

        private static bool _initialized;
        private static readonly object _initLock = new object();
        private static GetRedlinesDelegate _getRedlines;
        private static AssignRedlinesDelegate _assignRedlines;
        private static MergeRedlinesDelegate _mergeRedlines;

        private static PropertyInfo _handleProp;
        private static bool _handlePropChecked;
        private static readonly object _propLock = new object();

        static NativeClashRedlineHelper()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveNavisworksAssemblies;
            }
            catch
            {
                // Ignore
            }
        }

        private static Assembly ResolveNavisworksAssemblies(object sender, ResolveEventArgs args)
        {
            try
            {
                string asmName = new AssemblyName(args.Name).Name;
                if (asmName.StartsWith("Autodesk.Navisworks", StringComparison.OrdinalIgnoreCase))
                {
                    string nwPath = Path.Combine(@"C:\Program Files\Autodesk\Navisworks Manage 2024", asmName + ".dll");
                    if (File.Exists(nwPath))
                    {
                        return Assembly.LoadFrom(nwPath);
                    }

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string libPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "lib", "2024", asmName + ".dll"));
                    if (File.Exists(libPath))
                    {
                        return Assembly.LoadFrom(libPath);
                    }
                }
            }
            catch
            {
                // Fail silently
            }
            return null;
        }

        private static IntPtr GetNativeHandle(object obj)
        {
            if (obj == null) return IntPtr.Zero;
            try
            {
                if (!_handlePropChecked)
                {
                    lock (_propLock)
                    {
                        if (!_handlePropChecked)
                        {
                            Type t = obj.GetType();
                            while (t != null && t != typeof(object))
                            {
                                var prop = t.GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (prop != null)
                                {
                                    _handleProp = prop;
                                    break;
                                }
                                t = t.BaseType;
                            }
                            _handlePropChecked = true;
                        }
                    }
                }

                if (_handleProp != null)
                {
                    object val = _handleProp.GetValue(obj, null);
                    if (val is IntPtr ptr) return ptr;
                }
            }
            catch
            {
                // Defensive
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Public property to check if native symbols were successfully initialized.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                EnsureInitialized();
                return _initialized;
            }
        }

        public static bool HasNativeDelegates
        {
            get
            {
                EnsureInitialized();
                return _getRedlines != null && (_assignRedlines != null || _mergeRedlines != null);
            }
        }

        public static void EnsureInitialized(ILoggerService logger = null)
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                try
                {
                    // 1. Locate or load lcodclash.dll
                    IntPtr hClash = GetModuleHandle("lcodclash.dll");
                    if (hClash == IntPtr.Zero)
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string clashDllPath = Path.Combine(baseDir, "lcodclash.dll");
                        if (File.Exists(clashDllPath))
                        {
                            hClash = LoadLibrary(clashDllPath);
                        }
                    }

                    // 2. Locate or load lcodyplugin.dll
                    IntPtr hPlugin = GetModuleHandle("lcodyplugin.dll");
                    if (hPlugin == IntPtr.Zero)
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string pluginDllPath = Path.Combine(baseDir, "lcodyplugin.dll");
                        if (File.Exists(pluginDllPath))
                        {
                            hPlugin = LoadLibrary(pluginDllPath);
                        }
                    }

                    if (hClash != IntPtr.Zero)
                    {
                        IntPtr pGetRedlines = GetProcAddress(hClash, "?GetRedlines@LcOclTestIssue@@QEBAAEBVLcOpRedlineList@@XZ");
                        if (pGetRedlines != IntPtr.Zero)
                        {
                            _getRedlines = (GetRedlinesDelegate)Marshal.GetDelegateForFunctionPointer(
                                pGetRedlines, typeof(GetRedlinesDelegate));
                        }
                    }

                    if (hPlugin != IntPtr.Zero)
                    {
                        IntPtr pAssign = GetProcAddress(hPlugin, "??4LcOpRedlineList@@QEAAAEAV0@AEBV0@@Z");
                        if (pAssign != IntPtr.Zero)
                        {
                            _assignRedlines = (AssignRedlinesDelegate)Marshal.GetDelegateForFunctionPointer(
                                pAssign, typeof(AssignRedlinesDelegate));
                        }

                        IntPtr pMerge = GetProcAddress(hPlugin, "?Merge@LcOpRedlineList@@QEAAXPEAV1@@Z");
                        if (pMerge != IntPtr.Zero)
                        {
                            _mergeRedlines = (MergeRedlinesDelegate)Marshal.GetDelegateForFunctionPointer(
                                pMerge, typeof(MergeRedlinesDelegate));
                        }
                    }

                    logger?.Log($"[NativeClashRedlineHelper] Initialized: GetRedlines={_getRedlines != null}, Assign={_assignRedlines != null}, Merge={_mergeRedlines != null}");
                }
                catch (Exception ex)
                {
                    logger?.LogError("[NativeClashRedlineHelper] Initialization warning", ex);
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        /// <summary>
        /// Copies all redline annotations (ellipses, clouds, texts, etc.) and comments from a ClashResult
        /// or ClashResultGroup into the target SavedViewpoint.
        /// </summary>
        public static void CopyRedlinesAndComments(IClashResult result, SavedViewpoint svp, ILoggerService logger = null)
        {
            if (result == null || svp == null) return;
            EnsureInitialized(logger);

            try
            {
                // 1. Copy Comments
                CopyComments(result, svp);

                // 2. Process Redlines
                if (result is ClashResultGroup grp)
                {
                    bool anyCopied = false;

                    // A. Check if the group itself has redlines
                    if (grp.HasRedlines)
                    {
                        IntPtr src = GetSourceRedlinesPtr(grp);
                        IntPtr dest = GetDestinationRedlinesPtr(svp);
                        if (src != IntPtr.Zero && dest != IntPtr.Zero)
                        {
                            CopyRedlineData(dest, src, false);
                            anyCopied = true;
                            logger?.Log($"[NativeClashRedlineHelper] Copied group redlines for '{grp.DisplayName}'");
                        }
                    }

                    // B. Also inspect children: if someone added redlines to a child clash in the group
                    if (grp.Children != null)
                    {
                        foreach (var child in grp.Children.OfType<ClashResult>())
                        {
                            CopyComments(child, svp);

                            if (child.HasRedlines)
                            {
                                IntPtr src = GetSourceRedlinesPtr(child);
                                IntPtr dest = GetDestinationRedlinesPtr(svp);
                                if (src != IntPtr.Zero && dest != IntPtr.Zero)
                                {
                                    CopyRedlineData(dest, src, anyCopied);
                                    anyCopied = true;
                                    logger?.Log($"[NativeClashRedlineHelper] Copied child clash redlines from '{child.DisplayName}' into group viewpoint");
                                }
                            }
                        }
                    }
                }
                else if (result is ClashResult raw)
                {
                    // Copy raw clash redlines
                    if (raw.HasRedlines)
                    {
                        IntPtr src = GetSourceRedlinesPtr(raw);
                        IntPtr dest = GetDestinationRedlinesPtr(svp);
                        if (src != IntPtr.Zero && dest != IntPtr.Zero)
                        {
                            CopyRedlineData(dest, src, false);
                            logger?.Log($"[NativeClashRedlineHelper] Copied clash redlines for '{raw.DisplayName}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"[NativeClashRedlineHelper] Error copying redlines/comments for '{result.DisplayName}'", ex);
            }
        }

        private static IntPtr GetSourceRedlinesPtr(IClashResult result)
        {
            if (result == null) return IntPtr.Zero;

            IntPtr handle = GetNativeHandle(result);
            if (handle == IntPtr.Zero) return IntPtr.Zero;

            if (_getRedlines != null)
            {
                try
                {
                    return _getRedlines(handle);
                }
                catch
                {
                    // Fall back to known memory offset
                }
            }

            // Fallback: In all Navisworks 64-bit builds, LcOclTestIssue::GetRedlines returns (this + 0x198)
            try
            {
                return new IntPtr(handle.ToInt64() + 0x198);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static IntPtr GetDestinationRedlinesPtr(SavedViewpoint svp)
        {
            if (svp == null) return IntPtr.Zero;
            try
            {
                var redlines = svp.EditRedlines();
                return GetNativeHandle(redlines);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private static void CopyRedlineData(IntPtr destPtr, IntPtr srcPtr, bool merge)
        {
            if (destPtr == IntPtr.Zero || srcPtr == IntPtr.Zero) return;
            try
            {
                if (merge && _mergeRedlines != null)
                {
                    _mergeRedlines(destPtr, srcPtr);
                }
                else if (_assignRedlines != null)
                {
                    _assignRedlines(destPtr, srcPtr);
                }
                else if (_mergeRedlines != null)
                {
                    _mergeRedlines(destPtr, srcPtr);
                }
            }
            catch
            {
                // Defensive: fail gracefully
            }
        }

        private static void CopyComments(IClashResult source, SavedViewpoint svp)
        {
            if (source?.Comments == null || source.Comments.Count == 0 || svp?.Comments == null) return;
            try
            {
                foreach (Comment c in source.Comments)
                {
                    if (c == null) continue;
                    bool exists = false;
                    try
                    {
                        foreach (Comment existing in svp.Comments)
                        {
                            if (existing != null && existing.Body == c.Body && existing.Author == c.Author)
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore collection enumeration issues
                    }

                    if (!exists)
                    {
                        svp.Comments.Add(new Comment(c));
                    }
                }
            }
            catch
            {
                // Defensive
            }
        }
    }
}
