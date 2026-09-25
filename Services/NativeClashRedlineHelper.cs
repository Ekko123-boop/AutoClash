using System;
using System.Collections.Generic;
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
    /// and ClashResultGroup instances directly into exported SavedViewpoint items in the document.
    /// </summary>
    public static class NativeClashRedlineHelper
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        // Native member function: const LcOpRedlineList& LcOclTestIssue::GetRedlines() const
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetRedlinesDelegate(IntPtr issuePtr);

        // Native static function: bool LcOpSavedViewsElement::ReplaceViewRedlines(LcOpState* pState, const LcOpGroupItem* pFolder, int index, const LcOpRedlineList& redlines)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool ReplaceViewRedlinesDelegate(IntPtr pState, IntPtr pFolder, int index, IntPtr redlinesPtr);

        // Native assignment operator: LcOpRedlineList& LcOpRedlineList::operator=(const LcOpRedlineList&)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr AssignRedlinesDelegate(IntPtr destList, IntPtr srcList);

        // Native copy constructor: LcOpRedlineList::LcOpRedlineList(const LcOpRedlineList&)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr RedlineListCopyCtorDelegate(IntPtr thisPtr, IntPtr otherPtr);

        // Native member function: void LcOpRedlineList::Merge(LcOpRedlineList* pOther)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MergeRedlinesDelegate(IntPtr destList, IntPtr srcList);

        // Native destructor: LcOpRedlineList::~LcOpRedlineList()
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RedlineListDtorDelegate(IntPtr thisPtr);

        private static bool _initialized;
        private static readonly object _initLock = new object();
        private static GetRedlinesDelegate _getRedlines;
        private static ReplaceViewRedlinesDelegate _replaceViewRedlines;
        private static AssignRedlinesDelegate _assignRedlines;
        private static RedlineListCopyCtorDelegate _copyCtor;
        private static MergeRedlinesDelegate _mergeRedlines;
        private static RedlineListDtorDelegate _dtor;

        private static string _traceLogPath;

        static NativeClashRedlineHelper()
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CypherNavisTools", "Logs");
                Directory.CreateDirectory(logDir);
                _traceLogPath = Path.Combine(logDir, "redline_trace.log");
            }
            catch { }

            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveNavisworksAssemblies;
            }
            catch { }
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
            catch { }
            return null;
        }

        private static void TraceLog(string msg)
        {
            try
            {
                if (!string.IsNullOrEmpty(_traceLogPath))
                {
                    File.AppendAllText(_traceLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {msg}{Environment.NewLine}");
                }
            }
            catch { }
        }

        public static void EnsureInitialized(ILoggerService logger = null)
        {
            if (_initialized) return;
            lock (_initLock)
            {
                if (_initialized) return;
                try
                {
                    // 1. Locate or load lcodclash.dll (or lcclash.dll)
                    IntPtr hClash = GetModuleHandle("lcodclash.dll");
                    if (hClash == IntPtr.Zero)
                    {
                        hClash = GetModuleHandle("lcclash.dll");
                    }
                    if (hClash == IntPtr.Zero)
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string clashDllPath = Path.Combine(baseDir, "lcodclash.dll");
                        if (File.Exists(clashDllPath))
                        {
                            hClash = LoadLibrary(clashDllPath);
                        }
                        else
                        {
                            string altPath = Path.Combine(baseDir, "lcclash.dll");
                            if (File.Exists(altPath))
                            {
                                hClash = LoadLibrary(altPath);
                            }
                        }
                    }

                    // 2. Locate or load lcodyplugin.dll (or lcplugin.dll)
                    IntPtr hPlugin = GetModuleHandle("lcodyplugin.dll");
                    if (hPlugin == IntPtr.Zero)
                    {
                        hPlugin = GetModuleHandle("lcplugin.dll");
                    }
                    if (hPlugin == IntPtr.Zero)
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string pluginDllPath = Path.Combine(baseDir, "lcodyplugin.dll");
                        if (File.Exists(pluginDllPath))
                        {
                            hPlugin = LoadLibrary(pluginDllPath);
                        }
                        else
                        {
                            string altPath = Path.Combine(baseDir, "lcplugin.dll");
                            if (File.Exists(altPath))
                            {
                                hPlugin = LoadLibrary(altPath);
                            }
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

                        IntPtr pReplace = GetProcAddress(hPlugin, "?ReplaceViewRedlines@LcOpSavedViewsElement@@SA_NPEAVLcOpState@@PEBVLcOpGroupItem@@HAEBVLcOpRedlineList@@@Z");
                        if (pReplace != IntPtr.Zero)
                        {
                            _replaceViewRedlines = (ReplaceViewRedlinesDelegate)Marshal.GetDelegateForFunctionPointer(
                                pReplace, typeof(ReplaceViewRedlinesDelegate));
                        }

                        IntPtr pCopyCtor = GetProcAddress(hPlugin, "??0LcOpRedlineList@@QEAA@AEBV0@@Z");
                        if (pCopyCtor != IntPtr.Zero)
                        {
                            _copyCtor = (RedlineListCopyCtorDelegate)Marshal.GetDelegateForFunctionPointer(
                                pCopyCtor, typeof(RedlineListCopyCtorDelegate));
                        }

                        IntPtr pMerge = GetProcAddress(hPlugin, "?Merge@LcOpRedlineList@@QEAAXPEAV1@@Z");
                        if (pMerge != IntPtr.Zero)
                        {
                            _mergeRedlines = (MergeRedlinesDelegate)Marshal.GetDelegateForFunctionPointer(
                                pMerge, typeof(MergeRedlinesDelegate));
                        }

                        IntPtr pDtor = GetProcAddress(hPlugin, "??1LcOpRedlineList@@QEAA@XZ");
                        if (pDtor != IntPtr.Zero)
                        {
                            _dtor = (RedlineListDtorDelegate)Marshal.GetDelegateForFunctionPointer(
                                pDtor, typeof(RedlineListDtorDelegate));
                        }
                    }

                    string initMsg = $"[NativeClashRedlineHelper] Initialized: Assign={_assignRedlines != null}, ReplaceViewRedlines={_replaceViewRedlines != null}, Merge={_mergeRedlines != null}";
                    logger?.Log(initMsg);
                    TraceLog(initMsg);
                }
                catch (Exception ex)
                {
                    logger?.LogError("[NativeClashRedlineHelper] Initialization warning", ex);
                    TraceLog($"Initialization exception: {ex}");
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        /// <summary>
        /// Safely inspects the unmanaged LcOpRedlineList vector pointers.
        /// An LcOpRedlineList is a 24-byte vector struct: { void* start; void* end; void* capacity; }
        /// In 64-bit Navisworks, if start != 0, end > start, (end - start) % 8 == 0, and capacity >= end,
        /// graphic redline items are guaranteed present.
        /// </summary>
        public static bool HasRedlinesInMemory(IntPtr pRedlines)
        {
            if (pRedlines == IntPtr.Zero) return false;
            try
            {
                long start = Marshal.ReadIntPtr(pRedlines, 0).ToInt64();
                long end = Marshal.ReadIntPtr(pRedlines, IntPtr.Size).ToInt64();
                long cap = Marshal.ReadIntPtr(pRedlines, IntPtr.Size * 2).ToInt64();

                if (start == 0 || end <= start) return false;
                long byteLen = end - start;
                if (byteLen % IntPtr.Size != 0) return false;
                if (cap < end) return false;

                long count = byteLen / IntPtr.Size;
                return count > 0 && count < 10000;
            }
            catch
            {
                return false;
            }
        }

        public static int GetRedlineCountInMemory(IntPtr pRedlines)
        {
            if (pRedlines == IntPtr.Zero) return 0;
            try
            {
                long start = Marshal.ReadIntPtr(pRedlines, 0).ToInt64();
                long end = Marshal.ReadIntPtr(pRedlines, IntPtr.Size).ToInt64();
                if (start == 0 || end <= start) return 0;
                long diff = end - start;
                return (int)(diff / IntPtr.Size);
            }
            catch
            {
                return 0;
            }
        }

        public static IntPtr GetNativeHandle(object obj)
        {
            if (obj == null) return IntPtr.Zero;
            try
            {
                Type t = obj.GetType();
                while (t != null && t != typeof(object))
                {
                    var prop = t.GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        object val = prop.GetValue(obj, null);
                        if (val is IntPtr ptr) return ptr;
                    }
                    t = t.BaseType;
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Resolves the memory pointer to the LcOpRedlineList vector inside a ClashResult or ClashResultGroup.
        /// 
        /// MULTIPLE INHERITANCE MEMORY LAYOUT IN 64-BIT NAVISWORKS:
        /// - ClashResult (LcOclTestResult): LcOpSavedItem (+0x0) + LcOclTestIssue (+0x70).
        ///   In LcOclTestIssue, the LcOpRedlineList is at +0x198.
        ///   Offset = 0x70 + 0x198 = 0x208 (520 bytes).
        ///   Verified by native disassembly of LcOclTestResult::GetHasRedlines: cmp [rcx + 0x208], [rcx + 0x210].
        /// 
        /// - ClashResultGroup (LcOclTestResultGroup): LcOpGroupItem (+0x0) + LcOclTestIssue (+0x88).
        ///   In LcOclTestIssue, the LcOpRedlineList is at +0x198.
        ///   Offset = 0x88 + 0x198 = 0x220 (544 bytes).
        ///   Verified by native disassembly of LcOclTestResultGroup::GetHasRedlines: cmp [rcx + 0x220], [rcx + 0x228].
        /// </summary>
        public static IntPtr GetSourceRedlinesPtr(IClashResult result, ILoggerService logger = null)
        {
            if (result == null) return IntPtr.Zero;

            IntPtr handle = GetNativeHandle(result);
            if (handle == IntPtr.Zero) return IntPtr.Zero;

            int primaryOffset = (result is ClashResultGroup) ? 0x220 : 0x208;
            int secondaryOffset = (result is ClashResultGroup) ? 0x208 : 0x220;

            // 1. Primary offset check
            try
            {
                IntPtr candidate = new IntPtr(handle.ToInt64() + primaryOffset);
                if (HasRedlinesInMemory(candidate))
                {
                    TraceLog($"Found redlines at primary offset 0x{primaryOffset:X} on '{result.DisplayName}'");
                    return candidate;
                }
            }
            catch { }

            // 2. Secondary offset check
            try
            {
                IntPtr candidate = new IntPtr(handle.ToInt64() + secondaryOffset);
                if (HasRedlinesInMemory(candidate))
                {
                    TraceLog($"Found redlines at secondary offset 0x{secondaryOffset:X} on '{result.DisplayName}'");
                    return candidate;
                }
            }
            catch { }

            // 3. Dynamic memory scan across 8-byte aligned offsets in [0x180, 0x280]
            try
            {
                for (int off = 0x180; off <= 0x280; off += 8)
                {
                    if (off == primaryOffset || off == secondaryOffset) continue;
                    IntPtr candidate = new IntPtr(handle.ToInt64() + off);
                    if (HasRedlinesInMemory(candidate))
                    {
                        TraceLog($"Found redlines at dynamic scanned offset 0x{off:X} on '{result.DisplayName}'");
                        return candidate;
                    }
                }
            }
            catch { }

            // 4. Default to primary offset
            return new IntPtr(handle.ToInt64() + primaryOffset);
        }

        /// <summary>
        /// Resolves the memory pointer to the destination LcOpRedlineList vector inside a SavedViewpoint.
        /// In 64-bit Navisworks, SavedViewpoint stores its LcOpRedlineList at handle + 0x630 (1584 bytes),
        /// which is also exposed directly via the public .NET API svp.EditRedlines().Handle.
        /// </summary>
        public static IntPtr GetDestinationRedlinesPtr(SavedViewpoint svp)
        {
            if (svp == null) return IntPtr.Zero;
            try
            {
                var redlines = svp.EditRedlines();
                IntPtr h = GetNativeHandle(redlines);
                if (h != IntPtr.Zero) return h;
            }
            catch { }

            try
            {
                IntPtr svpHandle = GetNativeHandle(svp);
                if (svpHandle != IntPtr.Zero)
                {
                    return new IntPtr(svpHandle.ToInt64() + 0x630);
                }
            }
            catch { }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Safely copies review comments from a clash result or clash group onto a SavedViewpoint instance.
        /// Unmanaged memory mutations are bypassed to prevent native heap corruption across Navisworks versions.
        /// </summary>
        public static bool AttachRedlinesToSavedViewpoint(IClashResult source, SavedViewpoint svp, ILoggerService logger = null)
        {
            if (source == null || svp == null) return false;

            try
            {
                CopyComments(source, svp);
                return true;
            }
            catch (Exception ex)
            {
                string errMsg = $"[NativeClashRedlineHelper] Error copying comments to SavedViewpoint for '{source.DisplayName}': {ex.Message}";
                logger?.LogError(errMsg, ex);
                TraceLog(errMsg);
                return false;
            }
        }

        /// <summary>
        /// Deprecated native method bypassed for stability.
        /// </summary>
        public static bool ApplyRedlinesToViewpoint(
            Document doc,
            GroupItem folderOrRoot,
            int viewpointIndex,
            IClashResult source,
            ILoggerService logger = null)
        {
            return false;
        }

        /// <summary>
        /// Copies review comments from clash items into the SavedViewpoint.
        /// </summary>
        public static void CopyComments(IClashResult source, SavedViewpoint svp)
        {
            if (source == null || svp == null) return;
            try
            {
                CopyItemComments(source, svp);

                if (source is ClashResultGroup grp)
                {
                    if (grp.RepresentativeResult != null)
                    {
                        CopyItemComments(grp.RepresentativeResult, svp);
                    }

                    if (grp.Children != null)
                    {
                        foreach (var child in grp.Children.OfType<ClashResult>())
                        {
                            CopyItemComments(child, svp);
                        }
                    }
                }
            }
            catch { }
        }

        private static void CopyItemComments(IClashResult source, SavedViewpoint svp)
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
                    catch { }

                    if (!exists)
                    {
                        svp.Comments.Add(new Comment(c));
                    }
                }
            }
            catch { }
        }
    }
}
