document.addEventListener('DOMContentLoaded', async function () {
    var elems = document.querySelectorAll('.collapsible');
    var instances = M.Collapsible.init(elems);

    await UploadSavedTables(); // Загрузка сохраненных таблиц при загрузке страницы

    setInterval(updateData, 100); // Обновление каждые 100 миллисекунд
    updateData(); // Первоначальное обновление
});

let tables = [];
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

async function addTable() {                                           
    const tableId = document.getElementById('newTableId').value;
    const names = document.getElementById('newTableNames').value;
    const addresses = document.getElementById('addresses').value;
    const sizes = document.getElementById('newTableSizes').value;
    const types = document.getElementById('newTableTypes').value;
    const unitTypes = document.getElementById('newTableUnitTypes').value;
    const formats = document.getElementById('newTableFormats').value;
    const coiffients = document.getElementById('newTableCoiffients').value;

    if (!tableId || !addresses) {
        alert('Пожалуйста, заполните все поля.');
        return;
    }

    const namesArray = names.split(',').map(String);
    const addressesArray = addresses.split(',').map(Number);
    const sizesArray = sizes.split(',').map(Number);
    const typesArray = types.split(',').map(String);
    const unitTypesArray = unitTypes.split(',').map(String);
    const formatsArray = formats.split(',').map(String);
    const coiffientsArray = coiffients.split(',').map(Number);

    const response = await fetch(`/api/Table/AddNewTable`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ id: tableId, names: namesArray, addresses: addressesArray, sizes: sizesArray, types: typesArray, unitTypes: unitTypesArray, formats: formatsArray, coiffients: coiffientsArray })
    });

    if (response.ok) {
        const tableContainer = document.createElement('div');
        tableContainer.innerHTML =
            `
                    <section class="table-section">
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

                            <div class="table-section-log-control">
                                <span class="log-status">Лог отключен</span>
                                <button class="btn waves-effect waves-light blue">Логировать</button>
                                <button class="btn waves-effect waves-light blue">Выгрузить лог</button>
                            </div>
                        </div>
                    </section>
                `;
        document.getElementById('tablesContainer').appendChild(tableContainer);
        tables.push({ id: tableId, names: namesArray, addresses: addressesArray, sizes: sizesArray, types: typesArray, unitTypes: unitTypesArray, formats: formatsArray, coiffients: coiffientsArray });
    }
    else {
        alert('Ошибка при добавлении таблицы.');
    }
}

async function updateData() {
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
                        const tr = tableBody.querySelectorAll('tr')[index];

                        // Добавление столбца с названиями параметров
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


function applyCoefficient(value, coefficient) {
    if (coefficient > 0 && typeof value !== 'number') {
        return value / (10 * coefficient);
    }
    return value;
}

function handleFileUpload(event) {

    /*********                               ********* KEIL *********                                *********/

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
                        <section class="table-section">
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

                                <div class="table-section-log-control">
                                    <span class="log-status">Лог отключен</span>
                                    <button class="btn waves-effect waves-light blue">Логировать</button>
                                    <button class="btn waves-effect waves-light blue">Выгрузить лог</button>
                                </div>
                            </div>
                        </section>
                    `;
            document.getElementById('tablesContainer').appendChild(tableContainer);
            tables.push({ id: tableId, names: names, addresses: addresses, sizes: sizes, types: types, unitTypes: unitTypes, formats: formats, coiffients: coiffients });
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
        const { id, names, addresses, sizes, types, unitTypes, formats, coiffients} = tableData;

        const tableContainer = document.createElement('div');
        tableContainer.innerHTML =
            `
                <section class="table-section">
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

                        <div class="table-section-log-control">
                            <span class="log-status">Лог отключен</span>
                            <button class="btn waves-effect waves-light blue">Логировать</button>
                            <button class="btn waves-effect waves-light blue">Выгрузить лог</button>
                        </div>
                    </div>
                </section>
            `;
        document.getElementById('tablesContainer').appendChild(tableContainer);
        tables.push({ id, names, addresses, sizes, types, unitTypes, formats, coiffients});
    });
}
