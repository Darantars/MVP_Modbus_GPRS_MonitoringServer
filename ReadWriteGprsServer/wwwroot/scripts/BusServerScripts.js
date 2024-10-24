async function Home() {
    await fetch('/api/TCP/stop');
    window.location.href = '/Home';
}

async function StartConnection() {
    const connectionPort = document.getElementById('connectionPort').value;
    await fetch(`/api/TCP/start?connectionPort=${connectionPort}`);
}

async function StopConnection() {
    await fetch('/api/TCP/stop');
}

async function updateData() {
    const response = await fetch('/api/TCP/read');
    const data = await response.text();
    const newData = `<div>${data}</div>`;
    document.getElementById('data').innerHTML = newData;

    const response1 = await fetch('/api/TCP/updateConnectionStatus');
    const connection = await response1.text();
    const newStatus = `${connection}`;
    document.getElementById('connectionStatus').innerHTML = newStatus;
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
    const modbusID = document.getElementById('modbusID').value;
    const modbusStartAdress = document.getElementById('modbusStartAdress').value;
    const modbusInput =
    {
        modbusID: modbusID,
        modbusStartAdress: modbusStartAdress
    };
    const response = await fetch('/api/TCP/sendMb3',
        {
            method: 'POST',
            headers:
            {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(modbusInput)
        });
    if (response.ok) {
        alert('Modbus command sent successfully!');
    }
    else {
        const errorText = await response.text();
        alert('Failed to send Modbus command: ' + errorText);
    }
}

async function SendMb10WriteToDevice() {
    const modbusID = document.getElementById('modbusID').value;
    const modbusStartAdress = document.getElementById('modbusStartAdress').value;
    const modbussQuanity = document.getElementById('modbussQuanity').value;
    const modbussData = document.getElementById('modbussData').value;

    const modbusInput =
    {
        modbusID: modbusID,
        modbusStartAdress: modbusStartAdress,
        modbussQuanity: modbussQuanity,
        modbussData: modbussData
    };
    const response = await fetch('/api/TCP/sendMb10',
        {
            method: 'POST',
            headers:
            {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(modbusInput)
        });
    if (response.ok) {
        alert('Modbus command sent successfully!');
    }
    else {
        const errorText = await response.text();
        alert('Failed to send Modbus command: ' + errorText);
    }
}


setInterval(updateData, 200); // Обновление каждые 200 миллисекунд
updateData(); // Первоначальное обновление