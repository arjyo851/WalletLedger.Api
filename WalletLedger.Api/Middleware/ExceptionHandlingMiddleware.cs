namespace WalletLedger.Api.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next; //step1 variable _next of type request delegate and initialize it within constructor

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context) // In Invoke function of type Task we pass a HttpContext type variable through parameter
        {
            try
            {
                await _next(context); //within our requestdelegate we pass that httpcontext we got from the function
            }
            catch (InvalidOperationException ex)
            {
                // Treat InvalidOperationException as a server error in this API (tests expect 500 for domain errors)
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
            catch (Exception)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(
                    new { error = "Internal server error" }
                );
            }
        }
    }
}
