using Microsoft.AspNetCore.Http;

namespace ModernWMS.UnitTests.TestSupport
{
    /// <summary>
    /// Always reports no HttpContext, matching how FunctionHelper.GetCurrentUser() behaves
    /// outside a real request: it returns a default CurrentUser (tenant_id == 1) rather than
    /// the CurrentUser the test passed into the service under test. See the "FunctionHelper"
    /// note in _docs/unit-testing-plan.md's Phase 4 section.
    /// </summary>
    public sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext
        {
            get => null;
            set { }
        }
    }
}
