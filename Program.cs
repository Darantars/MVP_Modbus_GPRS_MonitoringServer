using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Read_Write_GPRS_Server.Controllers;
using System.Reflection.Emit;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Указываем IP-адрес и порт для прослушивания
string ipAddress = "90.188.113.113";
int port = 42362;
TcpConnectionController.TcpServer tcpServer = new TcpConnectionController.TcpServer(10000);

TcpConnectionController.TcpDeviceTable tcpDeviceTable = new TcpConnectionController.TcpDeviceTable();


app.MapGet("/", async (HttpContext context) =>
{
    var filePath = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "html", "index.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});



app.MapGet("/TCP-Server", async (HttpContext context) =>
{
    var filePath = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "html", "BusServer.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.MapGet("/api/TCP/start", async () =>
{
    await tcpServer.Start(ipAddress, port);
});

app.MapGet("/api/TCP/read", async () =>
{
    if (tcpServer.messageLog != null)
        return Results.Content(tcpServer.messageLog, "text/plain");
    else
        return Results.Content("Await press start to run server", "text/plain");
});

app.MapPost("/api/TCP/send", async (HttpContext context) =>
{
    var messageInput = await new StreamReader(context.Request.Body).ReadToEndAsync();
    await tcpServer.SendMessgeToDeviceASCII(messageInput);
    return Results.Ok();
});

app.MapGet("/api/TCP/updateConnectionStatus", async (HttpContext context) =>
{
        return Results.Content(tcpServer.GetDeviceConnectionStatus(), "text/plain");
});

app.MapPost("/api/TCP/sendMb3", async (HttpContext context) =>
{
    var mbData = await new StreamReader(context.Request.Body).ReadToEndAsync();

    try
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var modbusInput = JsonSerializer.Deserialize<TcpConnectionController.ModbusInput>(mbData, options);

        if (int.TryParse(modbusInput.ModbusReadID, out int ID) && int.TryParse(modbusInput.ModbusReadColumnNumber, out int ColumnNum))
        {
            await tcpServer.SendMB3CommandToDevice(ID, ColumnNum, 1);
            return Results.Ok();
        }
        else
        {
            return Results.BadRequest("Invalid input data");
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest("Invalid input data");
    }
});

app.MapGet("/api/TCP/stop", async () =>
{
    await tcpServer.Stop();
});








app.MapGet("/Table", async (HttpContext context) =>
{
    var filePath = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "html", "Table.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.MapGet("/api/Table/start", async () =>
{
    await tcpDeviceTable.Start(ipAddress, port);
});

app.MapGet("/api/Table/stop", async () =>
{
    await tcpDeviceTable.Stop();
});

app.MapGet("/api/Table/UpdateTable/{index}", async (int index) =>
{
        string answer = await tcpDeviceTable.GetMb3ParamValueAsync(202, index, 1);
        if (answer != "no data")
            return Results.Content(answer, "text/plain");
        return Results.Content("Не получен ответ от устройства", "text/plain");

});

app.MapGet("/api/Table/GetConnectionStatus", async () =>
{
    string answer = tcpDeviceTable.connectionStatus;
        return Results.Content(answer, "text/plain");


});

app.Run();