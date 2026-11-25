using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if NET
[assembly: System.Runtime.Versioning.SupportedOSPlatformAttribute("Windows7.0")]
#endif

// 将 ComVisible 设置为 false 会使此程序集中的类型
//对 COM 组件不可见。如果需要从 COM 访问此程序集中的类型
//请将此类型的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于类型库的 ID
[assembly: Guid("4b041b91-5790-4571-8c58-c63ffe4bc9f8")]

[assembly: InternalsVisibleTo("UnitTest")]
