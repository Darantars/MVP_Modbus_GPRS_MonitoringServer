
using System.Text.Json;
using Read_Write_GPRS_Server.Controllers;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Указываем IP-адрес и порт для прослушивания
string ipAddress = "90.188.113.113";
TcpConnectionController.TcpServer tcpServer = new TcpConnectionController.TcpServer(10000);

TcpConnectionController.TcpDeviceTableServer TcpDeviceTableServer = new TcpConnectionController.TcpDeviceTableServer();


app.MapGet("/", async (HttpContext context) =>
{
    var filePath = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "html", "Autorization.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.MapPost("/api/auth/login", async (HttpContext context) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        var jsonDocument = JsonDocument.Parse(json);
        var root = jsonDocument.RootElement;

        var username = root.GetProperty("username").GetString();
        var password = root.GetProperty("password").GetString();

        // Заданные логин и пароль
        const string validUsername = "MVadmin";
        const string validPassword = "NotNSO";

        Console.WriteLine(username + ":" + password);

        if (username == validUsername && password == validPassword)
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Login successful" }));
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Invalid username or password" }));
        }
    }
    catch (JsonException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Invalid JSON format" }));
    }
});


app.MapGet("/Home", async (HttpContext context) =>
{
    var filePath = Path.Combine(context.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath, "html", "Home.html");
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

app.MapPost("/api/Table/AddNewTable", async (HttpContext context) =>
{
    using var requestBody = context.Request.Body;
    var data = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(requestBody);

    if (data == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    if (data.TryGetValue("id", out var idObj)
        && data.TryGetValue("names", out var namesObj)
        && data.TryGetValue("addresses", out var addressesObj)
        && data.TryGetValue("sizes", out var sizesObj)
        && data.TryGetValue("types", out var typesObj)
        && data.TryGetValue("unitTypes", out var untTypesObj)
        && data.TryGetValue("formats", out var formatsObj))
    {
        if (idObj == null
            || namesObj == null
            || addressesObj == null
            || sizesObj == null
            || typesObj == null
            || untTypesObj == null
            || formatsObj == null)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        try
        {
            var id = idObj.ToString();
            var names = JsonSerializer.Deserialize<List<string>>(namesObj.ToString());
            var addresses = JsonSerializer.Deserialize<List<int>>(addressesObj.ToString());
            var sizes = JsonSerializer.Deserialize<List<int>>(sizesObj.ToString());
            var types = JsonSerializer.Deserialize<List<string>>(typesObj.ToString().ToLower());
            var unitTypes = JsonSerializer.Deserialize<List<string>>(untTypesObj.ToString());
            var formats = JsonSerializer.Deserialize<List<string>>(formatsObj.ToString().ToLower());

            if (names.Count != addresses.Count || names.Count != sizes.Count ||
                names.Count != types.Count || names.Count != unitTypes.Count ||
                names.Count != formats.Count)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("All lists must have the same length.");
                return;
            }

            await TcpDeviceTableServer.AddNewTable(id, 10, addresses.Count, names, addresses, sizes, types, unitTypes, formats);

            foreach (var table in TcpDeviceTableServer.dataTablesList)
            {
                Console.WriteLine(table.id);
            }
            context.Response.StatusCode = StatusCodes.Status200OK;
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync($"Error deserializing data: {ex.Message}");
            Console.WriteLine($"Error deserializing data: {ex.Message}");
        }
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});




app.MapGet("/api/Table/GetTableData", async (int modbusID, string tableId) =>
{
    if (TcpDeviceTableServer.dataTablesList != null)
    {
        var table = TcpDeviceTableServer.dataTablesList.FirstOrDefault(t => t.id == tableId);
        if (table != null)
        {
            await table.GetTableDataAsync("default", modbusID);
            var tableDataValues = table.GetTableDataValues();
            return Results.Json(tableDataValues);
        }
    }

    return Results.Json(new string[] { "Не опрашивается" });
});

app.MapGet("/api/Table/GetSavedTables", async () =>
{
    if (TcpDeviceTableServer.dataTablesList != null)
    {
        var tables = TcpDeviceTableServer.dataTablesList.Select(table => new
        {
            id = table.id,
            names = table.paramNames.ToArray(),
            addresses = table.paramAdreses.ToArray(),
            sizes = table.paramSizes.ToArray(),
            types = table.paramTypes.ToArray(),
            unitTypes = table.paramUnitTypes.ToArray(),
            formats = table.paramFormats.ToArray()
        }).ToList();


        return Results.Json(tables);
    }

    return Results.Json(new List<object>());
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