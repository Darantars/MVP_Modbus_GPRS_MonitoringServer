using System.Text.Json;
using Read_Write_GPRS_Server.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Read_Write_GPRS_Server.Db;
using Microsoft.AspNetCore.Hosting; // Add this namespace

// Указываем IP-адрес и порт для прослушивания
string ipAddress = "90.188.113.113";
TcpConnectionController.TcpServer tcpServer = new TcpConnectionController.TcpServer(10000);
TcpConnectionController.TcpDeviceTableServer TcpDeviceTableServer = new TcpConnectionController.TcpDeviceTableServer();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews(); // Эта строка необходима, если используются контроллеры и представления

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseAuthentication();
app.UseAuthorization();


app.MapGet("/", async (HttpContext context, IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.WebRootPath, "html", "Autorization.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.MapPost("/api/auth/login", async (HttpContext context, SignInManager<IdentityUser> signInManager) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        var jsonDocument = JsonDocument.Parse(json);
        var root = jsonDocument.RootElement;

        var email = root.GetProperty("email").GetString();
        var password = root.GetProperty("password").GetString();

        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded) // Потом убрать
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

app.MapPost("/api/auth/register", async (HttpContext context, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager) =>
{
    try
    {
        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        var jsonDocument = JsonDocument.Parse(json);
        var root = jsonDocument.RootElement;

        var email = root.GetProperty("email").GetString();
        var password = root.GetProperty("password").GetString();

        var user = new IdentityUser { UserName = email, Email = email };
        var result = await userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Registration successful" }));
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Registration failed", errors = result.Errors.Select(e => new { description = e.Description }).ToList() }));
        }
    }
    catch (JsonException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Invalid JSON format" }));
    }
});

app.MapGet("/Home", async (HttpContext context, IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.WebRootPath, "html", "Home.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.MapGet("/TCP-Server", async (HttpContext context, IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.WebRootPath, "html", "BusServer.html");
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

app.MapGet("/Table", async (HttpContext context, IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.WebRootPath, "html", "Table.html");
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
        && data.TryGetValue("formats", out var formatsObj)
        && data.TryGetValue("coiffients", out var coificentObj))
    {
        if (idObj == null
            || namesObj == null
            || addressesObj == null
            || sizesObj == null
            || typesObj == null
            || untTypesObj == null
            || formatsObj == null
            || coificentObj == null)
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
            var coificent = JsonSerializer.Deserialize<List<int>>(coificentObj.ToString());

            if (names.Count != addresses.Count || names.Count != sizes.Count ||
                names.Count != types.Count || names.Count != unitTypes.Count ||
                names.Count != formats.Count || formats.Count != coificent.Count)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("All lists must have the same length.");
                return;
            }
            await TcpDeviceTableServer.AddNewTable(id, 10, addresses.Count, names, addresses, sizes, types, unitTypes, formats, coificent);

            // Проверка наличия таблицы по tableid
            if (TcpDeviceTableServer.dataTablesList.Any(table => table.id == id))
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync($"Table with id '{id}' successfully added.");
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync($"Failed to add table with id '{id}'.");
            }
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

app.MapDelete("/api/Table/DeleteTable", async (string tableId) =>
{
    if (TcpDeviceTableServer.dataTablesList != null)
    {
        var table = TcpDeviceTableServer.dataTablesList.FirstOrDefault(t => t.id == tableId);
        if (table != null)
        {
            TcpDeviceTableServer.dataTablesList.Remove(table);
            return Results.Ok($"Table with id '{tableId}' successfully deleted.");
        }
    }

    return Results.NotFound($"Table with id '{tableId}' not found.");
});

app.MapGet("/api/Table/GetTableData", async (int modbusID, string tableId) =>
{
    if (TcpDeviceTableServer.dataTablesList != null)
    {
        var table = TcpDeviceTableServer.dataTablesList.FirstOrDefault(t => t.id == tableId);
        if (table != null)
        {
            await table.GetTableDataAsync(TcpDeviceTableServer.readingMode, modbusID);
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
            names = table.Parametrs.Select(param => param.name).ToArray(),
            addresses = table.Parametrs.Select(param => param.adress).ToArray(),
            sizes = table.Parametrs.Select(param => param.size).ToArray(),
            types = table.Parametrs.Select(param => param.type).ToArray(),
            unitTypes = table.Parametrs.Select(param => param.unitType).ToArray(),
            formats = table.Parametrs.Select(param => param.format).ToArray(),
            coiffients = table.Parametrs.Select(param => param.coiffient).ToArray()
        }).ToList();

        return Results.Json(tables);
    }

    return Results.Json(new List<object>());
});


app.MapGet("/api/Table/GetParameterValuesLast3Hours", async (string tableId, string parameterName) =>
{
    var table = TcpDeviceTableServer.dataTablesList.FirstOrDefault(t => t.id == tableId);
    if (table != null)
    {
        var values = await table.GetParameterValuesLast3Hours(parameterName);
        var result = values.Select(v => new
        {
            date = v.date.ToString("yyyy-MM-ddTHH:mm:ss"), // Формат даты для JSON
            value = v.value
        }).ToList();

        return Results.Json(result);
    }

    // Логирование для отладки
    Console.WriteLine($"Table with id {tableId} not found.");

    // Возвращаем заглушку, если таблица не найдена
    return Results.Json(new List<object>
    {
        new { date = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), value = "1:1" },
        new { date = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), value = "2:2" },
        new { date = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), value = "3:3" }
    });
});

app.MapGet("/api/Table/SwitchToBufferReadMode", async () =>
{
    TcpDeviceTableServer.readingMode = "buffer";
});

app.MapGet("/api/Table/SwitchToSingleReadMode", async () =>
{
    TcpDeviceTableServer.readingMode = "default";
});

app.MapGet("/api/Table/GetConnectionStatus", async () =>
{
    string answer = TcpDeviceTableServer.connectionStatus;
    return Results.Content(answer, "text/plain");
});

app.MapGet("/LiftView", async (HttpContext context, IWebHostEnvironment env) =>
{
    var filePath = Path.Combine(env.WebRootPath, "html", "LiftView.html");
    var htmlContent = await System.IO.File.ReadAllTextAsync(filePath);
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(htmlContent);
});

app.Run();
