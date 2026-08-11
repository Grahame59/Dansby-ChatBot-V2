const state = {
  mode: 'respond',
  printMode: false,
  lastRaw: '{}',
  mediaSettings: {
    moviePaths: [],
    tvShowPaths: []
  }
};

const modes = {
  respond: {
    label: 'Respond',
    caption: 'Respond returns the handler result immediately.',
    placeholder: 'Ask Dansby something...',
    endpoint: '/debug/respond'
  },
  route: {
    label: 'Route',
    caption: 'Route enqueues the recognized intent for background handling.',
    placeholder: 'Ask Dansby to route a task...',
    endpoint: '/intents'
  },
  recognize: {
    label: 'Recognize',
    caption: 'Recognize shows the detected intent, score, domain, and slots.',
    placeholder: 'Type text to classify...',
    endpoint: '/debug/recognize'
  },
  debug: {
    label: 'Debug Handle',
    caption: 'Debug posts a raw intent envelope directly to a handler.',
    placeholder: '{\n  "intent": "chat.greet",\n  "payload": {\n    "text": "hello"\n  }\n}',
    endpoint: '/debug/handle'
  }
};

const el = id => document.getElementById(id);
const val = id => el(id).value.trim();

document.addEventListener('DOMContentLoaded', () => {
  bindEvents();
  setMode('respond');
  updateKeyStatus();
  checkHealth();
});

function bindEvents() {
  document.querySelectorAll('[data-mode]').forEach(button => {
    button.addEventListener('click', () => setMode(button.dataset.mode));
  });

  el('apiKey').addEventListener('input', updateKeyStatus);
  el('runCommand').addEventListener('click', runCurrentMode);
  el('clearInput').addEventListener('click', clearInput);
  el('clearOutput').addEventListener('click', clearOutput);
  el('copyRaw').addEventListener('click', copyRaw);
  el('loadCsv').addEventListener('click', loadCsvToJson);
  el('debugTemplate').addEventListener('click', loadDebugTemplate);
  el('setPreview').addEventListener('click', () => setPrintMode(false));
  el('setPrint').addEventListener('click', () => setPrintMode(true));
  el('openSettings').addEventListener('click', openSettings);
  el('closeSettings').addEventListener('click', closeSettings);
  el('cancelSettings').addEventListener('click', closeSettings);
  el('reloadMediaSettings').addEventListener('click', loadMediaSettings);
  el('saveMediaSettings').addEventListener('click', saveMediaSettings);

  el('addMoviePath').addEventListener('click', () => {
    addPathRow('moviePathList', '');
  });

  el('addTvShowPath').addEventListener('click', () => {
    addPathRow('tvShowPathList', '');
  });

  el('settingsDialog').addEventListener('click', event => {
    if (event.target === el('settingsDialog')) {
      closeSettings();
    }
  });
}

function setMode(mode) {
  state.mode = mode;
  const config = modes[mode];

  document.querySelectorAll('[data-mode]').forEach(button => {
    button.classList.toggle('active', button.dataset.mode === mode);
  });

  el('modeCaption').textContent = config.caption;
  el('runCommand').textContent = config.label;

  const input = el('text');
  input.placeholder = config.placeholder;
  if (mode === 'debug' && !input.value.trim()) {
    input.value = config.placeholder;
  }
}

function updateKeyStatus() {
  const status = el('keyStatus');
  const hasKey = Boolean(val('apiKey'));

  status.textContent = hasKey ? 'Key: set' : 'Key: missing';
  status.className = `status-pill ${hasKey ? 'ok' : 'warn'}`;
}

async function checkHealth() {
  const status = el('healthStatus');

  try {
    const response = await fetch('/health');
    status.textContent = response.ok ? 'Health: online' : `Health: ${response.status}`;
    status.className = `status-pill ${response.ok ? 'ok' : 'bad'}`;
  } catch {
    status.textContent = 'Health: offline';
    status.className = 'status-pill bad';
  }
}

async function runCurrentMode() {
  const mode = state.mode;

  if (mode === 'debug') {
    let body;
    try {
      body = JSON.parse(el('text').value);
    } catch (error) {
      setOutput({
        ok: false,
        status: 'invalid json',
        endpoint: modes.debug.endpoint,
        body: String(error?.message || error)
      });
      return;
    }

    await post(modes.debug.endpoint, body);
    return;
  }

  const text = val('text');
  if (!text) {
    setOutput({
      ok: false,
      status: 'empty input',
      endpoint: modes[mode].endpoint,
      body: 'Input is empty.'
    });
    return;
  }

  if (mode === 'respond') {
    await post('/debug/respond', { text });
  } else if (mode === 'recognize') {
    await post('/debug/recognize', { text });
  } else if (mode === 'route') {
    await post('/intents', { intent: 'nlp.route', payload: { text } });
  }
}

async function post(endpoint, body) {
  setRequestStatus('Running', 'warn');
  el('lastEndpoint').textContent = endpoint;
  el('responseCode').textContent = '--';

  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Api-Key': val('apiKey')
      },
      body: JSON.stringify(body)
    });

    const text = await response.text();
    let parsed = null;

    try {
      parsed = text ? JSON.parse(text) : {};
    } catch {
      parsed = { response: text || '(empty response)' };
    }

    setOutput({
      ok: response.ok,
      status: response.status,
      endpoint,
      body: parsed
    });
  } catch (error) {
    setOutput({
      ok: false,
      status: 'request failed',
      endpoint,
      body: String(error?.message || error)
    });
  }
}

function setOutput(result) {
  const statusClass = result.ok ? 'ok' : 'bad';
  setRequestStatus(result.ok ? 'Complete' : 'Failed', statusClass);

  el('responseCode').textContent = String(result.status);
  const reply = extractReply(result.body);
  el('prettyReply').textContent = reply || summarizeResult(result);

  state.lastRaw = JSON.stringify(result.body, null, 2);
  el('out').textContent = state.lastRaw;
}

function setRequestStatus(text, statusClass) {
  const status = el('requestStatus');
  status.textContent = text;
  status.className = `status-pill ${statusClass}`;
}

function summarizeResult(result) {
  if (typeof result.body === 'string') return result.body;
  if (result.body?.intent) return `Intent: ${result.body.intent}`;
  if (result.body?.accepted) return `Accepted: ${result.body.correlationId}`;
  if (result.body?.error) return `${result.body.error}: ${result.body.message || ''}`.trim();
  return result.ok ? 'Done.' : 'Request failed.';
}

function extractReply(obj) {
  if (!obj || typeof obj !== 'object') return '';

  if (typeof obj.reply === 'string') return obj.reply;
  if (typeof obj.text === 'string') return obj.text;
  if (typeof obj.message === 'string') return obj.message;

  if (obj.result) {
    const reply = extractReply(obj.result);
    if (reply) return reply;
  }

  for (const key of ['outputs', 'messages', 'items', 'steps']) {
    if (!Array.isArray(obj[key])) continue;

    for (const item of obj[key]) {
      const reply = extractReply(item);
      if (reply) return reply;
    }
  }

  if (obj.data) {
    const reply = extractReply(obj.data);
    if (reply) return reply;
  }

  if (obj.payload) {
    const reply = extractReply(obj.payload);
    if (reply) return reply;
  }

  return '';
}

function clearInput() {
  el('text').value = '';
  setMode(state.mode);
}

function clearOutput() {
  state.lastRaw = '{}';
  el('prettyReply').textContent = 'Ready.';
  el('out').textContent = '{}';
  el('responseCode').textContent = '--';
  el('lastEndpoint').textContent = 'No request sent';
  setRequestStatus('Idle', '');
}

async function copyRaw() {
  try {
    await navigator.clipboard.writeText(state.lastRaw);
    setRequestStatus('Copied', 'ok');
  } catch {
    setRequestStatus('Copy failed', 'bad');
  }
}

function buildMailerJson(csvText, doPrint) {
  return {
    intent: 'zebra.print.mailer.from_csv',
    payload: {
      csvText,
      hasHeader: true,
      printReturnLabel: true,
      splitReturnToSeparateLabel: false,
      maxLabels: 999,
      doPrint: Boolean(doPrint)
    }
  };
}

async function loadCsvToJson() {
  const file = el('csvFile').files?.[0];
  if (!file) {
    setOutput({
      ok: false,
      status: 'missing file',
      endpoint: 'local',
      body: 'No CSV file selected.'
    });
    return;
  }

  const csvText = await file.text();
  el('text').value = JSON.stringify(buildMailerJson(csvText, state.printMode), null, 2);
  setMode('debug');
  setOutput({
    ok: true,
    status: 'loaded',
    endpoint: 'local',
    body: { loaded: true, rows: csvText.split(/\r?\n/).filter(Boolean).length, doPrint: state.printMode }
  });
}

function loadDebugTemplate() {
  el('text').value = JSON.stringify({
    intent: 'chat.greet',
    payload: {
      text: 'hello'
    }
  }, null, 2);
  setMode('debug');
}

function setPrintMode(enabled) {
  state.printMode = enabled;
  el('setPreview').classList.toggle('active', !enabled);
  el('setPrint').classList.toggle('active', enabled);

  try {
    const current = JSON.parse(el('text').value);
    if (current?.payload && Object.hasOwn(current.payload, 'doPrint')) {
      current.payload.doPrint = enabled;
      el('text').value = JSON.stringify(current, null, 2);
    }
  } catch {
    // Text area is not JSON; nothing to update.
  }

  async function openSettings() {
    const dialog = el('settingsDialog');

    if (!dialog.open) {
      dialog.showModal();
    }

    await loadMediaSettings();
  }

  function closeSettings() {
    const dialog = el('settingsDialog');

    if (dialog.open) {
      dialog.close();
    }

    setMediaSettingsStatus('');
  }

  async function loadMediaSettings() {
    setMediaSettingsBusy(true);
    setMediaSettingsStatus('Loading settings...');

    try {
      const response = await fetch('/api/settings/media', {
        method: 'GET',
        headers: buildApiHeaders()
      });

      const body = await readResponseBody(response);

      if (!response.ok) {
        throw new Error(getErrorMessage(body, `Unable to load settings (${response.status}).`));
      }

      const settings = normalizeMediaSettings(body);

      state.mediaSettings = {
        moviePaths: [...settings.moviePaths],
        tvShowPaths: [...settings.tvShowPaths]
      };

      renderPathList('moviePathList', settings.moviePaths);
      renderPathList('tvShowPathList', settings.tvShowPaths);
      setMediaSettingsStatus('Settings loaded.', 'ok');
    } catch (error) {
      setMediaSettingsStatus(
        String(error?.message || 'Unable to load media settings.'),
        'bad'
      );
    } finally {
      setMediaSettingsBusy(false);
    }
  }

  async function saveMediaSettings() {
    const settings = {
      moviePaths: collectPaths('moviePathList'),
      tvShowPaths: collectPaths('tvShowPathList')
    };

    setMediaSettingsBusy(true);
    setMediaSettingsStatus('Saving settings...');

    try {
      const response = await fetch('/api/settings/media', {
        method: 'PUT',
        headers: buildApiHeaders(true),
        body: JSON.stringify(settings)
      });

      const body = await readResponseBody(response);

      if (!response.ok) {
        throw new Error(getErrorMessage(body, `Unable to save settings (${response.status}).`));
      }

      const savedSettings = normalizeMediaSettings(body, settings);

      state.mediaSettings = {
        moviePaths: [...savedSettings.moviePaths],
        tvShowPaths: [...savedSettings.tvShowPaths]
      };

      renderPathList('moviePathList', savedSettings.moviePaths);
      renderPathList('tvShowPathList', savedSettings.tvShowPaths);
      setMediaSettingsStatus('Media settings saved.', 'ok');
    } catch (error) {
      setMediaSettingsStatus(
        String(error?.message || 'Unable to save media settings.'),
        'bad'
      );
    } finally {
      setMediaSettingsBusy(false);
    }
  }

  function buildApiHeaders(includeJson = false) {
    const headers = {};
    const apiKey = val('apiKey');

    if (includeJson) {
      headers['Content-Type'] = 'application/json';
    }

    if (apiKey) {
      headers['X-Api-Key'] = apiKey;
    }

    return headers;
  }

  async function readResponseBody(response) {
    const text = await response.text();

    if (!text) {
      return {};
    }

    try {
      return JSON.parse(text);
    } catch {
      return { message: text };
    }
  }

  function normalizeMediaSettings(body, fallback = {}) {
    const source =
      body?.settings?.media ??
      body?.media ??
      body?.settings ??
      body ??
      {};

    return {
      moviePaths: normalizePathArray(
        source.moviePaths ??
        source.movie_paths ??
        fallback.moviePaths
      ),
      tvShowPaths: normalizePathArray(
        source.tvShowPaths ??
        source.tv_show_paths ??
        source.tvPaths ??
        fallback.tvShowPaths
      )
    };
  }

  function normalizePathArray(paths) {
    if (!Array.isArray(paths)) {
      return [];
    }

    return paths
      .filter(path => typeof path === 'string')
      .map(path => path.trim())
      .filter(Boolean);
  }

  function renderPathList(listId, paths) {
    const list = el(listId);
    list.replaceChildren();

    if (!paths.length) {
      addPathRow(listId, '');
      return;
    }

    paths.forEach(path => addPathRow(listId, path));
  }

  function addPathRow(listId, path) {
    const list = el(listId);
    const row = document.createElement('div');
    const input = document.createElement('input');
    const removeButton = document.createElement('button');

    row.className = 'path-row';

    input.className = 'path-input';
    input.type = 'text';
    input.value = path;
    input.placeholder = getPathPlaceholder(listId);
    input.autocomplete = 'off';
    input.setAttribute('aria-label', getPathLabel(listId));

    removeButton.className = 'remove-path';
    removeButton.type = 'button';
    removeButton.textContent = 'Remove';
    removeButton.setAttribute('aria-label', `Remove ${getPathLabel(listId).toLowerCase()}`);

    removeButton.addEventListener('click', () => {
      row.remove();

      if (!list.children.length) {
        addPathRow(listId, '');
      }
    });

    row.append(input, removeButton);
    list.append(row);
  }

  function collectPaths(listId) {
    return [...el(listId).querySelectorAll('.path-input')]
      .map(input => input.value.trim())
      .filter(Boolean);
  }

  function getPathLabel(listId) {
    return listId === 'moviePathList' ? 'Movie path' : 'TV-show path';
  }

  function getPathPlaceholder(listId) {
    return listId === 'moviePathList'
      ? '/media/movies'
      : '/media/tv';
  }

  function setMediaSettingsStatus(message, statusClass = '') {
    const status = el('mediaSettingsStatus');
    status.textContent = message;
    status.className = `settings-message ${statusClass}`.trim();
  }

  function setMediaSettingsBusy(busy) {
    el('reloadMediaSettings').disabled = busy;
    el('saveMediaSettings').disabled = busy;
    el('addMoviePath').disabled = busy;
    el('addTvShowPath').disabled = busy;
  }

  function getErrorMessage(body, fallback) {
    if (typeof body === 'string' && body.trim()) {
      return body;
    }

    return body?.message || body?.error || fallback;
  }
}
