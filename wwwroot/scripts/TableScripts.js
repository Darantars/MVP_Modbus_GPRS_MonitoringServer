document.addEventListener('DOMContentLoaded', async function () {
    var elems = document.querySelectorAll('.collapsible');
    var instances = M.Collapsible.init(elems);

    await UploadSavedTables();

    setInterval(updateTableData, 0);
   /* setInterval(updateChartData, 1000);                  ВКЛЮЧИ*/

    // Первоначальное обновление
    updateTableData();
    updateChartData();

    // Инициализация переключателя
    const readMode = document.getElementById('readMode');
    readMode.addEventListener('change', async function () {
    if (this.checked) {
        await SwitchToBufferReadMode();
    } else {
        await SwitchToSingleReadMode();
    }
    });

    const connectionSwitch = document.getElementById('connectionSwitch');
    connectionSwitch.addEventListener('change', async function () {
        if (this.checked) {
            await StartConnection();
        } else {
            await StopConnection();
        }
    });
});

let tables = [];
let charts = [];
let updateToken = true;

async function Home() {
    StopConnection();
    window.location.href = '/Home';
}

async function StartConnection() {
    const connectionPort = document.getElementById('connectionPort').value;
    await fetch(`/api/Table/start?connectionPort=${connectionPort}`);
}

async function StopConnection() {
    await fetch('/api/Table/stop');
}

async function SwitchToSingleReadMode() {
    await fetch('/api/Table/SwitchToSingleReadMode');
}

async function SwitchToBufferReadMode() {
    await fetch('/api/Table/SwitchToBufferReadMode');
}

function createChartForTable(tableId, parameterNames) {
    const ctx = document.getElementById(`chart-${tableId}`).getContext('2d');
    const chart = new Chart(ctx, {
        type: 'line',
        data: {
            datasets: parameterNames.map(name => ({
                label: name,
                data: [],
                borderColor: getRandomColor(),
                fill: false
            }))
        },
        options: {
            responsive: true,
            scales: {
                x: [{
                    type: 'time',
                    time: {
                        unit: 'second',
                        displayFormats: {
                            second: 'HH:mm:ss'
                        }
                    }
                }]
            },
            ticks: {
                source: 'auto',
                callback: function (value, index, values) {
                    // Проверяем, есть ли уже такая метка времени
                    if (index > 0 && values[index - 1] === value) {
                        return ''; // Возвращаем пустую строку, чтобы избежать дублирования
                    }
                    return value;
                }
            }
        }
    });

    return chart;
}

function getRandomColor() {
    const letters = '0123456789ABCDEF';
    let color = '#';
    for (let i = 0; i < 6; i++) {
        color += letters[Math.floor(Math.random() * 16)];
    }
    return color;
}

async function addTable() {
    const tableContainer = document.createElement('div');
    tableContainer.innerHTML =
        `
        <section class="table-section" id="${tableId}" >
            <h4 class="table-section-header">${tableId}</h4>
            <div class="table-section-body">
                <table>
                    <thead>
                        <tr>
                            <th>Название параметра</th>
                            <th>Значение</th>
                            <th>Ед.изм</th>
                            <th>Адрес</th>
                            <th>Формат</th>
                            <th>Вид</th>
                            <th>Размер</th>
                            <th>Койфициент</th>
                        </tr>
                    </thead>
                    <tbody id="${tableId}">
                        ${namesArray.map((name, index) => `
                            <tr>
                                <td>${name}</td>
                                <td>Не опрашивается</td>
                                <td>${unitTypesArray[index]}</td>
                                <td>${addressesArray[index]}</td>
                                <td>${formatsArray[index]}</td>
                                <td>${typesArray[index]}</td>
                                <td>${sizesArray[index]}</td>
                                <td>${coiffientsArray[index]}</td>
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
                <div>
                    <canvas id="chart-${tableId}" width="800" height="1200"></canvas>
                </div>
                <div class="table-section-log-control">
                    <span class="log-status">Лог отключен</span>
                    <button class="btn waves-effect waves-light blue">Логировать</button>
                    <button class="btn waves-effect waves-light blue">Выгрузить лог</button>
                    <button class="btn waves-effect waves-light red" onclick="deleteTable('${tableId}')">Удалить таблицу</button>
                </div>
            </div>
        </section>
        `;
    document.getElementById('tablesContainer').appendChild(tableContainer);
    tables.push({ id: tableId, names: namesArray, addresses: addressesArray, sizes: sizesArray, types: typesArray, unitTypes: unitTypesArray, formats: formatsArray, coiffients: coiffientsArray });

    // Создание графика
    const chart = createChartForTable(tableId, namesArray);
    charts.push({ id: tableId, chart: chart });
}

async function deleteTable(tableId) {
    const response = await fetch(`/api/Table/DeleteTable?tableId=${tableId}`, {
        method: 'DELETE'
    });

    if (response.ok) {
        // Удаление таблицы из DOM
        const tableSection = document.querySelector(`section[id="${tableId}"]`);
        if (tableSection) {
            tableSection.remove();
        }

        // Удаление таблицы из массива tables
        tables = tables.filter(table => table.id !== tableId);

        // Удаление графика из массива charts
        charts = charts.filter(chart => chart.id !== tableId);

    } else {
        alert('Ошибка при удалении таблицы.');
    }
}

async function updateTableData() {
    const response = await fetch(`/api/Table/GetConnectionStatus`);
    const connectionStatus = await response.text();
    document.getElementById(`conectionStatus`).innerHTML = connectionStatus;

    if (updateToken === true) {
        updateToken = false;

        // *** Работа с таблицей ***
        const modbusID = document.getElementById('modbusID').value;
        if (modbusID != "") {
            for (const table of tables) {
                const tableId = table.id;
                const tableDataResponse = await fetch(`/api/Table/GetTableData?modbusID=${modbusID}&tableId=${tableId}`);
                const tableData = await tableDataResponse.json();

                const tableBody = document.getElementById(tableId);

                if (tableData.includes("Не опрашивается")) {

                    if (!tableBody) {
                        console.error(`Table with id ${tableId} not found`);
                        continue;
                    }

                    const tr = tableBody.querySelectorAll('tr')[index];
                    if (!tr) {
                        console.error(`Row at index ${index} not found in table with id ${tableId}`);
                        continue;
                    }

                    const tdValue = tr.querySelectorAll('td')[1];
                    if (!tdValue) {
                        console.error(`Cell at index 1 not found in row ${index} of table with id ${tableId}`);
                        continue;
                    }

                    // Заполнение таблицы строками с "Не опрашивается"
                    table.names.forEach((name, index) => {
                        const tr = tableBody.querySelectorAll('tr')[index];

                        // Добавление столбца с названиями параметров
                        const tdValue = tr.querySelectorAll('td')[1];
                        tdValue.textContent = "Не опрашивается";
                    });
                } else {
                    // Заполнение таблицы реальными данными
                    tableData.forEach((row, index) => {
                        const tr = tableBody.querySelectorAll('tr')[index + 1];
                        const tdValue = tr.querySelectorAll('td')[1];
                        const coefficient = table.coiffients[index];
                        tdValue.textContent = applyCoefficient(row, coefficient);

                    });
                }
            }
        }

        updateToken = true;
    }
}

async function updateChartData() {
    // *** Работа с графиками ***
    for (const table of tables) {
        const tableId = table.id;
        const chartEntry = charts.find(c => c.id === tableId);
        if (chartEntry) {
            const chart = chartEntry.chart;
            await freshChartData(chart);    // TODO: очень неоптимально, переделать
            for (const name of table.names) {
                console.log(`Fetching parameter values for ${name}...`); // Отладочное сообщение
                try {
                    const parameterValuesResponse = await fetch(`/api/Table/GetParameterValuesLast3Hours?tableId=${tableId}&parameterName=${name}`);
                    const data = await parameterValuesResponse.json();
                    let dataset = chart.data.datasets.find(ds => ds.label === name);
                    if (!dataset) {
                        console.warn(`Dataset not found for parameter ${name}`);
                        continue;
                    }

                    // Сортировка данных по времени
                    data.sort((a, b) => new Date(a.date) - new Date(b.date));

                    // Найти минимальное и максимальное время
                    const minTime = new Date(data[0].date);
                    const maxTime = new Date(data[data.length - 1].date);

                    // Добавить данные с временными метками и "пустыми" точками
                    let currentTime = new Date(minTime);
                    while (currentTime <= maxTime) {
                        const item = data.find(d => new Date(d.date).getTime() === currentTime.getTime());
                        const value = item ? parseFloat(item.value.replace(",", ".")) : null;
                        addData(chart, currentTime, value, name);
                        currentTime.setSeconds(currentTime.getSeconds() + 1);
                    }

                    chart.update();
                } catch (error) {
                    console.error(`Error fetching parameter values for ${name}:`, error);
                }
            }
        }
    }
}

function parseCustomDate(dateString) {
    const [day, month, year, hours, minutes, seconds] = dateString.match(/\d+/g);
    if (!day || !month || !year || !hours || !minutes || !seconds) {
        console.error(`Invalid date string: ${dateString}`);
        return null;
    }
    return new Date(`${hours}:${minutes}:${seconds}`);
}

async function freshChartData(chart) {
    chart.data.labels = [];   // Очистка коллекции меток
    await chart.data.datasets.forEach(dataset => {
        dataset.data = []; // Очистка данных в каждом наборе данных
    });
    await chart.update();
}

function addData(chart, label, newData, parameterName) {
    chart.data.labels.push(label);
    const dataset = chart.data.datasets.find(ds => ds.label === parameterName);
    if (dataset) {
        dataset.data.push(newData);
    }
    chart.update();
}

function removeData(chart) {
    chart.data.labels.pop();
    chart.data.datasets.forEach((dataset) => {
        dataset.data.pop();
    });
    chart.update();
}

function applyCoefficient(value, coefficient) {
    if (coefficient > 0 && typeof parseFloat(value) == 'number') {
        return parseFloat(value.replace(',', '.')).toFixed(6) / (10 ** coefficient);
    }
    return value;
}

function handleFileUpload(event) {
    const file = event.target.files[0];
    const fileName = file.name.replace(/\.[^/.]+$/, ""); // Удаляем расширение файла
    const reader = new FileReader();

    reader.onload = function (e) {
        const data = new Uint8Array(e.target.result);
        const workbook = XLSX.read(data, { type: 'array' });
        const sheetName = workbook.SheetNames[0];
        const worksheet = workbook.Sheets[sheetName];
        const jsonData = XLSX.utils.sheet_to_json(worksheet, { header: 1 });

        // Пропускаем первую строку (заголовок)
        const dataRows = jsonData.slice(1);

        let currentTableId = null;
        let currentTable = null;
        let currentGroupName = null;

        dataRows.forEach(row => {
            const idParts = row[0].split('.');
            const tableIdPart = idParts[0];
            const groupName = row[1];
            const parameter = idParts[2];

            const tableId = `${fileName}    ${groupName}`;

            if (tableIdPart !== currentTableId) {
                if (currentTable) {
                    addTableFromData(`${fileName}   ${currentGroupName}`, currentTable.names, currentTable.addresses, currentTable.sizes, currentTable.types, currentTable.unitTypes, currentTable.formats, currentTable.coiffients);
                }
                currentTableId = tableIdPart;
                currentGroupName = groupName;
                currentTable = {
                    names: [],
                    addresses: [],
                    sizes: [],
                    types: [],
                    unitTypes: [],
                    formats: [],
                    coiffients: []
                };
            }

            currentTable.names.push(row[1]);
            currentTable.unitTypes.push(row[3]);
            currentTable.types.push(row[5]);
            currentTable.formats.push(row[4]);
            currentTable.addresses.push(row[6]);
            currentTable.sizes.push(row[12]);
            currentTable.coiffients.push(row[11]);
        });

        if (currentTable) {
            addTableFromData(`${fileName}   ${currentGroupName}`, currentTable.names, currentTable.addresses, currentTable.sizes, currentTable.types, currentTable.unitTypes, currentTable.formats, currentTable.coiffients);
        }
    };

    reader.readAsArrayBuffer(file);
}

async function addTableFromData(tableId, names, addresses, sizes, types, unitTypes, formats, coiffients) {
    try {
        // Преобразование строк в числа и избавление от null
        addresses = addresses.map(addr => addr == null || addr == "" || addr == "null" ? 0 : parseInt(addr, 10));
        sizes = sizes.map(size => size == null || size == "" || size == "null" ? 0 : parseInt(size, 10));
        types = types.map(type => type == null ? "" : type);
        unitTypes = unitTypes.map(unit => unit == null ? "" : unit);
        formats = formats.map(format => format == null ? "" : format);
        names = names.map(name => name == null ? "" : name);
        coiffients = coiffients.map(coif => coif == null || coif == "" || coif == "null" ? 0 : parseInt(coif, 10));

        const jsonData = JSON.stringify({
            id: tableId,
            names: names,
            addresses: addresses,
            sizes: sizes,
            types: types,
            unitTypes: unitTypes,
            formats: formats,
            coiffients: coiffients
        });
        console.log('JSON данные для отправки:', jsonData);
        const response = await fetch(`/api/Table/AddNewTable`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                id: tableId,
                names: names,
                addresses: addresses,
                sizes: sizes,
                types: types,
                unitTypes: unitTypes,
                formats: formats,
                coiffients: coiffients
            })
        });

        if (response.ok) {
            const tableContainer = document.createElement('div');
            tableContainer.innerHTML =
                `
                    <section class="table-section" id="${tableId}" >
                            <h4 class="table-section-header">${tableId}</h4>
                            <div class="table-section-body">
                                <table>
                                    <thead>
                                        <tr>
                                            <th>Название параметра</th>
                                            <th>Значение</th>
                                            <th>Ед.изм</th>
                                            <th>Адрес</th>
                                            <th>Формат</th>
                                            <th>Вид</th>
                                            <th>Размер</th>
                                            <th>Койфициент</th>
                                        </tr>
                                    </thead>
                                    <tbody id="${tableId}">
                                        ${names.map((name, index) => `
                                            <tr>
                                                <td>${name}</td>
                                                <td>Не опрашивается</td>
                                                <td>${unitTypes[index]}</td>
                                                <td>${addresses[index]}</td>
                                                <td>${formats[index]}</td>
                                                <td>${types[index]}</td>
                                                <td>${sizes[index]}</td>
                                                <td>${coiffients[index]}</td>
                                            </tr>
                                        `).join('')}
                                    </tbody>
                                </table>

                                <div>
                                    <canvas id="chart-${tableId}" width="800" height="1200"></canvas>
                                </div>
                                <div class="table-section-log-control">
                                    <span class="log-status">Лог отключен</span>
                                    <button class="btn waves-effect waves-light blue">Логировать</button>
                                    <button class="btn waves-effect waves-light blue">Выгрузить лог</button>
                                    <button class="btn waves-effect waves-light red" onclick="deleteTable('${tableId}')">Удалить таблицу</button>
                                </div>
                            </div>
                        </section>
                    `;
            document.getElementById('tablesContainer').appendChild(tableContainer);
            tables.push({ id: tableId, names: names, addresses: addresses, sizes: sizes, types: types, unitTypes: unitTypes, formats: formats, coiffients: coiffients });

            // Создание графика
            const chart = createChartForTable(tableId, names);
            charts.push({ id: tableId, chart: chart });
        } else {
            alert('Ошибка при добавлении таблицы.');
        }
    } catch (error) {
        console.error('Ошибка при отправке данных на сервер:', error);
        alert('Произошла ошибка при отправке данных на сервер. Пожалуйста, проверьте формат данных.');
    }
}

async function UploadSavedTables() {
    const response = await fetch('/api/Table/GetSavedTables');
    const tablesData = await response.json();

    tablesData.forEach(tableData => {
        const { id, names, addresses, sizes, types, unitTypes, formats, coiffients } = tableData;

        const tableContainer = document.createElement('div');
        tableContainer.innerHTML =
            `
                <section class="table-section" id="${id}" >
                    <h4 class="table-section-header">${id}</h4>
                    <div class="table-section-body">
                        <table>
                            <thead>
                                <tr>
                                    <th>Название параметра</th>
                                    <th>Значение</th>
                                    <th>Ед.изм</th>
                                    <th>Адрес</th>
                                    <th>Формат</th>
                                    <th>Вид</th>
                                    <th>Размер</th>
                                    <th>Койфициент</th>
                                </tr>
                            </thead>
                            <tbody id="${id}">
                                ${names.map((name, index) => `
                                    <tr>
                                        <td>${name}</td>
                                        <td>Не опрашивается</td>
                                        <td>${unitTypes[index]}</td>
                                        <td>${addresses[index]}</td>
                                        <td>${formats[index]}</td>
                                        <td>${types[index]}</td>
                                        <td>${sizes[index]}</td>
                                        <td>${coiffients[index]}</td>
                                    </tr>
                                `).join('')}
                            </tbody>
                        </table>

                        <div>
                            <canvas id="chart-${id}" width="800" height="1200"></canvas>
                        </div>
                        <div class="table-section-log-control">
                            <span class="log-status">Лог отключен</span>
                            <button class="btn waves-effect waves-light blue">Логировать</button>
                            <button class="btn waves-effect waves-light blue">Выгрузить лог</button>
                            <button class="btn waves-effect waves-light red" onclick="deleteTable('${id}')">Удалить таблицу</button>
                        </div>
                    </div>
                </section>
            `;
        document.getElementById('tablesContainer').appendChild(tableContainer);
        tables.push({ id, names, addresses, sizes, types, unitTypes, formats, coiffients });

        // Создание графика
        const chart = createChartForTable(id, names);
        charts.push({ id: id, chart: chart });
    });
}


