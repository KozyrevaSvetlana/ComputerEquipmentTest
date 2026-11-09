(function () {
  'use strict';

  const API = '/api';
  const ALL_TYPES = 0; // ключ пункта «Вся техника» в каталоге

  DevExpress.localization.locale('ru');

  const state = {
    typeId: null,   // выбранный тип; null — все типы
    search: '',
    types: [],      // [{ id, name, count }]
    total: 0,
    limit: 0,
    editingId: null,
    saving: false,
  };

  // ---------- HTTP ----------

  async function http(method, url, body) {
    const response = await fetch(url, {
      method,
      headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });

    if (response.status === 204) return null;

    const text = await response.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* не JSON */ }

    if (!response.ok) {
      const error = new Error(problemMessage(json, response.status));
      error.status = response.status;
      error.problem = json;
      throw error;
    }
    return json;
  }

  function problemMessage(problem, status) {
    if (problem && problem.errors) {
      return Object.values(problem.errors).flat().join('\n');
    }
    if (problem && (problem.detail || problem.title)) {
      return problem.detail || problem.title;
    }
    return 'Ошибка сервера (' + status + ')';
  }

  function notify(message, type) {
    DevExpress.ui.notify({ message, type: type || 'success', displayTime: 3000, width: 'auto' },
      { position: 'bottom center', direction: 'up-push' });
  }

  // ---------- Источник данных таблицы: пагинация и сортировка на сервере ----------

  function normalizeSort(sort) {
    const items = Array.isArray(sort) ? sort : [sort];
    return items.map(s => typeof s === 'string'
      ? { selector: s, desc: false }
      : { selector: s.selector, desc: !!s.desc });
  }

  const store = new DevExpress.data.CustomStore({
    key: 'id',
    load(options) {
      const params = new URLSearchParams();
      if (options.skip != null) params.set('skip', options.skip);
      if (options.take != null) params.set('take', options.take);
      if (options.sort) params.set('sort', JSON.stringify(normalizeSort(options.sort)));
      if (state.typeId) params.set('typeId', state.typeId);
      if (state.search) params.set('search', state.search);

      return http('GET', API + '/equipment?' + params.toString())
        .catch(err => { notify(err.message, 'error'); throw err; });
    },
  });

  // ---------- Каталог (слева) ----------

  const catalogList = $('#catalog-list').dxList({
    keyExpr: 'id',
    selectionMode: 'single',
    focusStateEnabled: false,
    noDataText: 'Типов пока нет',
    itemTemplate(item) {
      return $('<div class="catalog-item">').append(
        $('<span class="catalog-item__name">').text(item.name).attr('title', item.name),
        $('<span class="catalog-item__count">').text(item.count));
    },
    onSelectionChanged(e) {
      if (!e.addedItems.length) return;
      const id = e.addedItems[0].id;
      setTypeFilter(id === ALL_TYPES ? null : id);
    },
  }).dxList('instance');

  $('#add-type-button').dxButton({
    icon: 'add',
    hint: 'Добавить тип техники',
    stylingMode: 'text',
    onClick: () => openTypeEditor(),
  });

  const deleteTypeButton = $('#delete-type-button').dxButton({
    icon: 'trash',
    hint: 'Удалить выбранный тип (только если в нем нет техники)',
    stylingMode: 'text',
    disabled: true,
    onClick: () => deleteSelectedType(),
  }).dxButton('instance');

  // ---------- Таблица ----------

  let typeFilterBox = null;
  let addButton = null;
  let searchTimer = null;

  const grid = $('#grid').dxDataGrid({
    dataSource: store,
    remoteOperations: { paging: true, sorting: true },
    showBorders: false,
    showColumnLines: false,
    rowAlternationEnabled: true,
    hoverStateEnabled: true,
    columnAutoWidth: true,
    wordWrapEnabled: true,
    sorting: { mode: 'multiple' },
    paging: { pageSize: 10 },
    pager: {
      visible: true,
      showPageSizeSelector: true,
      allowedPageSizes: [10, 20, 50, 100],
      showInfo: true,
      infoText: 'Страница {0} из {1} (записей: {2})',
      showNavigationButtons: true,
    },
    noDataText: 'Техники не найдено',
    columns: [
      {
        dataField: 'inventoryNumber',
        caption: 'Учетный номер',
        cellTemplate(container, options) {
          $('<span class="inventory-number">').text(options.value).appendTo(container);
        },
      },
      { dataField: 'typeName', caption: 'Тип техники' },
      { dataField: 'name', caption: 'Наименование' },
      { dataField: 'room', caption: 'Комната размещения', dataType: 'number', alignment: 'center', width: 180 },
      {
        type: 'buttons',
        width: 90,
        buttons: [
          { hint: 'Редактировать', icon: 'edit', onClick: e => openEditor(e.row.data) },
          { hint: 'Удалить', icon: 'trash', onClick: e => confirmDelete(e.row.data) },
        ],
      },
    ],
    // Редактирование: двойной щелчок, кнопка в строке, контекстное меню.
    onRowDblClick(e) {
      if (e.rowType === 'data') openEditor(e.data);
    },
    onContextMenuPreparing(e) {
      if (e.target !== 'content' || !e.row || e.row.rowType !== 'data') return;
      const data = e.row.data;
      e.items = [
        { text: 'Редактировать', icon: 'edit', onItemClick: () => openEditor(data) },
        { text: 'Удалить', icon: 'trash', onItemClick: () => confirmDelete(data) },
      ];
    },
    toolbar: {
      items: [
        {
          location: 'before',
          widget: 'dxSelectBox',
          options: {
            width: 240,
            placeholder: 'Все типы',
            items: [],
            valueExpr: 'id',
            displayExpr: 'name',
            showClearButton: true,
            searchEnabled: true,
            inputAttr: { 'aria-label': 'Тип техники' },
            onInitialized(e) { typeFilterBox = e.component; },
            onValueChanged(e) { setTypeFilter(e.value || null); },
          },
        },
        {
          location: 'before',
          widget: 'dxTextBox',
          options: {
            width: 260,
            mode: 'search',
            placeholder: 'Поиск по номеру или наименованию',
            valueChangeEvent: 'input',
            onValueChanged(e) {
              clearTimeout(searchTimer);
              searchTimer = setTimeout(() => {
                state.search = (e.value || '').trim();
                reloadGrid();
              }, 300);
            },
          },
        },
        {
          location: 'after',
          widget: 'dxButton',
          options: {
            text: 'Добавить технику',
            icon: 'add',
            type: 'default',
            onInitialized(e) { addButton = e.component; },
            onClick: () => openEditor(null),
          },
        },
        {
          location: 'after',
          widget: 'dxButton',
          options: { icon: 'refresh', hint: 'Обновить', onClick: () => refreshAll() },
        },
      ],
    },
  }).dxDataGrid('instance');

  function reloadGrid() {
    grid.option('paging.pageIndex', 0);
    return grid.refresh();
  }

  function setTypeFilter(typeId) {
    if (state.typeId === typeId) return;
    state.typeId = typeId;

    // Синхронизируем каталог и выпадающий список, чтобы они показывали одно и то же.
    if (typeFilterBox && typeFilterBox.option('value') !== typeId) typeFilterBox.option('value', typeId);
    const listKey = typeId === null ? ALL_TYPES : typeId;
    if (catalogList.option('selectedItemKeys')[0] !== listKey) catalogList.option('selectedItemKeys', [listKey]);

    updateTitle();
    deleteTypeButton.option('disabled', !canDeleteSelectedType());
    reloadGrid();
  }

  function selectedType() {
    return state.types.find(t => t.id === state.typeId) || null;
  }

  function canDeleteSelectedType() {
    const type = selectedType();
    return !!type && type.count === 0;
  }

  function updateTitle() {
    const type = selectedType();
    $('#content-title').text(type ? type.name : 'Вся техника');
  }

  async function loadCatalog() {
    const catalog = await http('GET', API + '/catalog');
    state.types = catalog.types;
    state.total = catalog.total;
    state.limit = catalog.limit;

    // Если выбранный тип удалили — возвращаемся ко всем типам.
    if (state.typeId !== null && !selectedType()) {
      state.typeId = null;
      reloadGrid();
    }

    catalogList.option('items', [{ id: ALL_TYPES, name: 'Вся техника', count: catalog.total }].concat(catalog.types));
    catalogList.option('selectedItemKeys', [state.typeId === null ? ALL_TYPES : state.typeId]);
    if (typeFilterBox) {
      typeFilterBox.option('items', catalog.types);
      typeFilterBox.option('value', state.typeId);
    }
    typeEditorBox().option('items', catalog.types);

    const full = catalog.total >= catalog.limit;
    $('#counter')
      .text('Записей: ' + catalog.total + ' из ' + catalog.limit)
      .toggleClass('app-counter--full', full);
    if (addButton) {
      addButton.option('disabled', full);
      addButton.option('hint', full ? 'Достигнут лимит записей' : '');
    }
    deleteTypeButton.option('disabled', !canDeleteSelectedType());
    updateTitle();
  }

  function refreshAll() {
    return Promise.all([loadCatalog(), grid.refresh()])
      .catch(err => notify(err.message, 'error'));
  }

  // ---------- Диалог карточки техники ----------

  let saveButton = null;

  const editorPopup = $('#editor-popup').dxPopup({
    width: 560,
    height: 'auto',
    showTitle: true,
    title: '',
    dragEnabled: true,
    showCloseButton: true,
    hideOnOutsideClick: false,
    deferRendering: false,
    contentTemplate() {
      return $('<div>').append(
        $('<div id="editor-error" class="form-error" role="alert">'),
        $('<div id="editor-form">'));
    },
    toolbarItems: [
      {
        widget: 'dxButton', toolbar: 'bottom', location: 'after',
        options: {
          text: 'Сохранить', type: 'default',
          onInitialized(e) { saveButton = e.component; },
          onClick: () => saveEquipment(),
        },
      },
      {
        widget: 'dxButton', toolbar: 'bottom', location: 'after',
        options: { text: 'Отмена', onClick: () => editorPopup.hide() },
      },
    ],
    onShown() { editorForm.getEditor('inventoryNumber').focus(); },
  }).dxPopup('instance');

  const editorForm = $('#editor-form').dxForm({
    formData: {},
    validationGroup: 'equipment-form',
    labelLocation: 'top',
    showColonAfterLabel: false,
    colCount: 1,
    items: [
      {
        dataField: 'inventoryNumber',
        label: { text: 'Учетный номер' },
        editorOptions: { maxLength: 32, placeholder: 'Например, PC0003', inputAttr: { style: 'text-transform: uppercase' } },
        validationRules: [
          { type: 'required', message: 'Укажите учетный номер' },
          { type: 'pattern', pattern: /^[0-9A-Za-z]{1,32}$/, message: 'Только цифры и латинские буквы, до 32 символов' },
        ],
      },
      {
        dataField: 'name',
        label: { text: 'Наименование' },
        editorOptions: { maxLength: 256 },
        validationRules: [
          { type: 'required', message: 'Укажите наименование' },
          { type: 'stringLength', max: 256, message: 'Не длиннее 256 символов' },
        ],
      },
      {
        dataField: 'typeId',
        editorType: 'dxSelectBox',
        label: { text: 'Тип техники' },
        editorOptions: { items: [], valueExpr: 'id', displayExpr: 'name', searchEnabled: true, placeholder: 'Выберите тип' },
        validationRules: [{ type: 'required', message: 'Выберите тип техники' }],
      },
      {
        dataField: 'room',
        editorType: 'dxNumberBox',
        label: { text: 'Комната размещения' },
        editorOptions: { min: 1, max: 1000, showSpinButtons: true, format: '#0' },
        validationRules: [
          { type: 'required', message: 'Укажите комнату' },
          { type: 'range', min: 1, max: 1000, message: 'Целое число от 1 до 1000' },
        ],
      },
    ],
  }).dxForm('instance');

  function typeEditorBox() {
    return editorForm.getEditor('typeId');
  }

  function showFormError(message) {
    $('#editor-error').text(message || '').toggleClass('form-error--visible', !!message);
  }

  async function openEditor(row) {
    showFormError('');
    let data;

    if (row) {
      // Берем свежую версию с сервера: строка в таблице могла устареть.
      try {
        data = await http('GET', API + '/equipment/' + row.id);
      } catch (err) {
        notify(err.message, 'error');
        refreshAll();
        return;
      }
      state.editingId = data.id;
      editorPopup.option('title', 'Редактирование: ' + data.inventoryNumber);
    } else {
      if (state.total >= state.limit) {
        notify('Достигнут лимит в ' + state.limit + ' записей', 'warning');
        return;
      }
      state.editingId = null;
      data = { inventoryNumber: '', name: '', typeId: state.typeId, room: null };
      editorPopup.option('title', 'Новая техника');
    }

    // Сбрасываем подсветку ошибок валидации с прошлого открытия, затем подставляем данные.
    DevExpress.validationEngine.resetGroup('equipment-form');
    editorForm.option('formData', Object.assign({}, data));
    editorPopup.show();
  }

  async function saveEquipment() {
    if (state.saving) return; // защита от двойного клика
    if (!editorForm.validate().isValid) return;

    const d = editorForm.option('formData');
    const body = {
      inventoryNumber: (d.inventoryNumber || '').trim(),
      name: (d.name || '').trim(),
      typeId: d.typeId,
      room: d.room,
    };

    setSaving(true);
    showFormError('');
    try {
      if (state.editingId) {
        body.version = d.version;
        await http('PUT', API + '/equipment/' + state.editingId, body);
        notify('Изменения сохранены');
      } else {
        await http('POST', API + '/equipment', body);
        notify('Запись добавлена');
      }
      editorPopup.hide();
      refreshAll();
    } catch (err) {
      showFormError(err.message);
      if (err.status === 409) refreshAll();
    } finally {
      setSaving(false);
    }
  }

  function setSaving(value) {
    state.saving = value;
    if (saveButton) saveButton.option('disabled', value);
  }

  async function confirmDelete(row) {
    const ok = await DevExpress.ui.dialog.confirm(
      'Удалить запись <b>' + escapeHtml(row.inventoryNumber) + '</b> — ' + escapeHtml(row.name) + '?',
      'Удаление');
    if (!ok) return;
    try {
      await http('DELETE', API + '/equipment/' + row.id);
      notify('Запись удалена');
    } catch (err) {
      notify(err.message, 'error');
    }
    refreshAll();
  }

  function escapeHtml(value) {
    return $('<div>').text(value == null ? '' : String(value)).html();
  }

  // ---------- Диалог нового типа техники ----------

  const typePopup = $('#type-popup').dxPopup({
    width: 420,
    height: 'auto',
    showTitle: true,
    title: 'Новый тип техники',
    showCloseButton: true,
    deferRendering: false,
    contentTemplate() {
      return $('<div>').append(
        $('<div id="type-error" class="form-error" role="alert">'),
        $('<div id="type-form">'));
    },
    toolbarItems: [
      {
        widget: 'dxButton', toolbar: 'bottom', location: 'after',
        options: { text: 'Добавить', type: 'default', onClick: () => saveType() },
      },
      {
        widget: 'dxButton', toolbar: 'bottom', location: 'after',
        options: { text: 'Отмена', onClick: () => typePopup.hide() },
      },
    ],
    onShown() { typeForm.getEditor('name').focus(); },
  }).dxPopup('instance');

  const typeForm = $('#type-form').dxForm({
    formData: { name: '' },
    labelLocation: 'top',
    items: [{
      dataField: 'name',
      label: { text: 'Название' },
      editorOptions: { maxLength: 128 },
      validationRules: [
        { type: 'required', message: 'Укажите название' },
        { type: 'stringLength', max: 128, message: 'Не длиннее 128 символов' },
      ],
    }],
  }).dxForm('instance');

  function openTypeEditor() {
    $('#type-error').text('').removeClass('form-error--visible');
    typeForm.option('formData', { name: '' });
    typePopup.show();
  }

  async function saveType() {
    if (!typeForm.validate().isValid) return;
    try {
      const created = await http('POST', API + '/equipment-types', { name: typeForm.option('formData').name.trim() });
      typePopup.hide();
      notify('Тип «' + created.name + '» добавлен');
      await loadCatalog();
    } catch (err) {
      $('#type-error').text(err.message).addClass('form-error--visible');
    }
  }

  async function deleteSelectedType() {
    const type = selectedType();
    if (!type) return;
    const ok = await DevExpress.ui.dialog.confirm('Удалить тип <b>' + escapeHtml(type.name) + '</b>?', 'Удаление типа');
    if (!ok) return;
    try {
      await http('DELETE', API + '/equipment-types/' + type.id);
      notify('Тип удален');
    } catch (err) {
      notify(err.message, 'error');
    }
    loadCatalog().catch(err => notify(err.message, 'error'));
  }

  // ---------- Старт ----------

  loadCatalog().catch(err => notify('Не удалось загрузить каталог: ' + err.message, 'error'));
})();
