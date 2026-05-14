var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Run(HandleRequestAsync);

app.Run();

static Task HandleRequestAsync(HttpContext context)
{
    if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/")
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync("Byrd Feature Flags Admin", context.RequestAborted);
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
}
