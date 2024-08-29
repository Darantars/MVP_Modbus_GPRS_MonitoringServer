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
int port = 42360;
TcpConnectionController.TcpServer tcpServer = new TcpConnectionController.TcpServer();
TcpConnectionController.TcpDeviceTable tcpDeviceTable = new TcpConnectionController.TcpDeviceTable();

app.MapGet("/", async () =>
{
    return Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
    <title>GPRS</title>
    <!-- Materialize CSS -->
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/css/materialize.min.css"" rel=""stylesheet"">
    <!-- Materialize JavaScript -->
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/js/materialize.min.js""></script>
    <script>
        async function Server() {
            window.location.href = '/TCP-Server';
        }
        async function Table() {
            window.location.href = '/Table';
        }
    </script>
</head>
<body>
    <div class=""container"">
        <button class=""btn waves-effect waves-light"" onclick=""Server()"">Server</button>
        <button class=""btn waves-effect waves-light"" onclick=""Table()"">Table</button>
    </div>
</body>
</html>
", "text/html");
});

app.MapGet("/TCP-Server", () =>
{
    return Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
    <title>GPRS-Server</title>
    <!-- Materialize CSS -->
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/css/materialize.min.css"" rel=""stylesheet"">
    <!-- Materialize JavaScript -->
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/js/materialize.min.js""></script>
    <script>
        async function Table() {
            await fetch('/api/TCP/stop');
            window.location.href = '/Table';
        }

        async function StartConnection() {
            await fetch('/api/TCP/start');
        }

        async function StopConnection() {
            await fetch('/api/TCP/stop');
        }

        async function updateData() {
            const response = await fetch('/api/TCP/read');
            const data = await response.text();
            const newData = `<div>${data}</div>`;
            document.getElementById('data').innerHTML = newData;
        }

        async function SendToDevice() {
            const message = document.getElementById('messageInput').value;
            const response = await fetch('/api/TCP/send', {
                method: 'POST',
                headers: {
                    'Content-Type': 'text/plain'
                },
                body: message
            });

            if (response.ok) {
                alert('Message sent successfully!');
            } else {
                const errorText = await response.text();
                alert('Failed to send message: ' + errorText);
            }
        }

        async function SendMb3ReadToDevice() {
            const modbusReadID = document.getElementById('modbusReadID').value;
            const modbusReadColumnNumber = document.getElementById('modbusReadColumnNumber').value;
            const modbusInput = {
                modbusReadID: modbusReadID,
                modbusReadColumnNumber: modbusReadColumnNumber
            };

            console.log(modbusInput); // ƒобавьте это дл€ отладки

            const response = await fetch('/api/TCP/sendMb3', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(modbusInput)
            });

            if (response.ok) {
                alert('Modbus command sent successfully!');
            } else {
                const errorText = await response.text();
                alert('Failed to send Modbus command: ' + errorText);
            }
        }

        setInterval(updateData, 200); // ќбновление каждые 200 миллисекунд
        updateData(); // ѕервоначальное обновление
    </script>
</head>
<body>
    <div class=""container"">
        <button class=""btn waves-effect waves-light"" onclick=""Table()"">Table</button>
        <button class=""btn waves-effect waves-light"" onclick=""StartConnection()"">Start</button>
        <button class=""btn waves-effect waves-light"" onclick=""StopConnection()"">Stop</button>
        <h1 class=""center-align"">GPRS-Server:</h1>
        <div id='sendZone' class=""row"">
            <div class=""input-field col s12"">
                <input type=""text"" id=""messageInput"" placeholder=""Enter your message"">
                <button class=""btn waves-effect waves-light"" onclick=""SendToDevice()"">Send Message</button>
            </div>
        </div>
        <div id='ModbusReadComand' class=""row"">
            <div class=""input-field col s6"">
                <input type=""number"" id=""modbusReadID"" placeholder=""ModbusID"">
            </div>
            <div class=""input-field col s6"">
                <input type=""number"" id=""modbusReadColumnNumber"" placeholder=""ColumnNumber"">
            </div>
            <div class=""col s12"">
                <button class=""btn waves-effect waves-light"" onclick=""SendMb3ReadToDevice()"">Send MB Read 3 Command</button>
            </div>
        </div>
        <div id='data' class=""row""></div>
    </div>
</body>
</html>
", "text/html");
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
        return Results.Content("Loading...", "text/plain");
});

app.MapPost("/api/TCP/send", async (HttpContext context) =>
{
    var messageInput = await new StreamReader(context.Request.Body).ReadToEndAsync();
    await tcpServer.SendMessgeToDeviceASCII(messageInput);
    return Results.Ok();
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

app.MapGet("/Table", async () =>
{
    return Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"" />
    <title>Table</title>
    <!-- Materialize CSS -->
    <link href=""https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/css/materialize.min.css"" rel=""stylesheet"">
    <!-- Materialize JavaScript -->
    <script src=""https://cdnjs.cloudflare.com/ajax/libs/materialize/1.0.0/js/materialize.min.js""></script>
    <script>
        async function Server() {
            StopConnection();
            window.location.href = '/TCP-Server';
        }
        async function StartConnection() {
            await fetch('/api/Table/start');
        }
        async function StopConnection() {
            await fetch('/api/Table/stop');
        }

        async function updateData() {
            const response = await fetch(`/api/Table/GetConnectionStatus`);
            const connectionStatus = await response.text();
                document.getElementById(`conectionStatus`).innerHTML = connectionStatus;

   
                for (let i = 1; i <= 10; i++) {
                    const response = await fetch(`/api/Table/UpdateTable/${i}`);
                    const data = await response.text();
                    document.getElementById(`dataTd${i}`).innerHTML = data;
                    await new Promise(resolve => setTimeout(resolve, 100));
                }
            
        }

        setInterval(updateData, 100); // ќбновление каждые 1000 миллисекунд
        updateData(); // ѕервоначальное обновление
    </script>
</head>
<body>
    <div class=""container"">
        <button class=""btn waves-effect waves-light"" onclick=""Server()"">Server</button>
        <button class=""btn waves-effect waves-light"" onclick=""StartConnection()"">Start</button>
        <button class=""btn waves-effect waves-light"" onclick=""StopConnection()"">Stop</button>
        <div>
            <h1 class=""center-align"">Device-Table:</h1>
            <h4 id=""conectionStatus""><h4>
             <input type=""number"" id=""modbusReadID"" placeholder=""ModbusID"">
        </div>
        <table>
            <tr><th>адресс</th><th>значение в uint16(dec)</th></tr>
            <tr><td>1</td><td id=""dataTd1"" >данные</td></tr>
            <tr><td>2</td><td id=""dataTd2"">данные</td></tr>
            <tr><td>3</td><td id=""dataTd3"">данные</td></tr>
            <tr><td>4</td><td id=""dataTd4"">данные</td></tr>
            <tr><td>5</td><td id=""dataTd5"">данные</td></tr>
            <tr><td>6</td><td id=""dataTd6"">данные</td></tr>
            <tr><td>7</td><td id=""dataTd7"">данные</td></tr>
            <tr><td>8</td><td id=""dataTd8"">данные</td></tr>
            <tr><td>9</td><td id=""dataTd9"">данные</td></tr>
            <tr><td>10</td><td id=""dataTd10"">данные</td></tr>
        </table>
    </div>
</body>
</html>
", "text/html");
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
        return Results.Content("Ќе получен ответ от устройства", "text/plain");

});

app.MapGet("/api/Table/GetConnectionStatus", async () =>
{
    string answer = tcpDeviceTable.connectionStatus;
        return Results.Content(answer, "text/plain");


});

app.Run();