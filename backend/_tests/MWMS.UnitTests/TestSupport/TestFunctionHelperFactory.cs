using ModernWMS.Core;
using ModernWMS.Core.DBContext;

namespace ModernWMS.UnitTests.TestSupport
{
    /// <summary>
    /// Builds a real FunctionHelper against a test SqlDBContext, backed by a
    /// NullHttpContextAccessor (no HTTP context, so FunctionHelper.GetCurrentUser()
    /// always resolves to the default CurrentUser, tenant_id == 1).
    /// </summary>
    public static class TestFunctionHelperFactory
    {
        public static FunctionHelper Create(SqlDBContext dbContext) =>
            new(dbContext, new NullHttpContextAccessor());
    }
}
