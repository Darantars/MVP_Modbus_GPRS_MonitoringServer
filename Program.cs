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

// ”казываем IP-адрес и порт дл€ прослушивани€
string ipAddress = "90.188.113.113";
TcpConnectionController.TcpServer tcpServer = new TcpConnectionController.TcpServer(10000);

TcpConnectionController.TcpDeviceTableServer TcpDeviceTableServer = new TcpConnectionController.TcpDeviceTableServer();


app.MapGet("/", async (HttpContext context) =>
{
    var filePath = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "html", "index.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.MapGet("/Home", async (HttpContext context) =>
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

app.MapGet("/api/TCP/start", async (int connectionPort) =>
{   
    await tcpServer.Start(ipAddress, connectionPort);
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

        if (int.TryParse(modbusInput.modbusID, out int ID) && int.TryParse(modbusInput.modbusStartAdress, out int ColumnNum))
        {
            await tcpServer.SendMB3CommandToDevice(tcpServer.device, ID, ColumnNum, 1);
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

app.MapPost("/api/TCP/sendMb10", async (HttpContext context) =>
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

        if (int.TryParse(modbusInput.modbusID, out int ID) 
        && int.TryParse(modbusInput.modbusStartAdress, out int ColumnNum)
        && int.TryParse(modbusInput.modbussQuanity, out int Quanity))
        {
            byte[] data = new byte[Quanity*2];
            string[] dataBytes = modbusInput.modbussData.Split('_');

            Console.WriteLine(modbusInput.modbusID + " " +  modbusInput.modbusStartAdress + " " + modbusInput.modbussQuanity + " " + dataBytes[0] + " " + dataBytes[1]);

            for (int i = 0; i < dataBytes.Count(); i++)
            {
                data[i] = Convert.ToByte(dataBytes[i]);
            }

            await tcpServer.SendMB10CommandToDevice(tcpServer.device, ID, ColumnNum, Quanity, data);
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

app.MapGet("/api/Table/start", async (int connectionPort) =>
{
    await TcpDeviceTableServer.Start(ipAddress, connectionPort);
});

app.MapGet("/api/Table/stop", async () =>
{
    await TcpDeviceTableServer.Stop();
});

app.MapGet("/api/Table/GetTableData", async (int modbusID) =>
{
    if(TcpDeviceTableServer.isRunning && TcpDeviceTableServer.dataTable != null)
    {
        await TcpDeviceTableServer.dataTable.GetTableDataAsync("default", modbusID);    
        var tableDataValues = TcpDeviceTableServer.dataTable.GetTableDataValues();
        return Results.Json(tableDataValues);
    }

    
    return Results.Json(new string[] { "Loading...", "Loading...", "Loading...", "Loading...", "Loading...",
        "Loading...", "Loading...", "Loading...", "Loading...", "Loading..." });
});

app.MapGet("/api/Table/GetConnectionStatus", async () =>
{
    string answer = TcpDeviceTableServer.connectionStatus;
        return Results.Content(answer, "text/plain");


});

app.MapGet("/LiftView", async (HttpContext context) =>
{
    var filePath = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "html", "LiftView.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.Run();