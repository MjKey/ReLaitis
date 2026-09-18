// ReLaitis Voice Bridge - Background Service Worker (Manifest V3)

const RELAITIS_WS_URL = 'ws://127.0.0.1:11337/ws';
let socket = null;
let reconnectTimer = null;
let isConnecting = false;

// Подключение к локальному серверу ReLaitis
function connectWebSocket() {
  if (isConnecting || (socket && socket.readyState === WebSocket.OPEN)) return;

  isConnecting = true;
  clearTimeout(reconnectTimer);

  try {
    socket = new WebSocket(RELAITIS_WS_URL);

    socket.onopen = () => {
      isConnecting = false;
      console.log('[ReLaitis] Успешно подключено к серверу ReLaitis');
      broadcastConnectionStatus(true);
      sendCurrentTabState();
    };

    socket.onmessage = async (event) => {
      try {
        const message = JSON.parse(event.data);
        await handleIncomingCommand(message);
      } catch (err) {
        console.error('[ReLaitis] Ошибка обработки сообщения:', err);
      }
    };

    socket.onclose = () => {
      isConnecting = false;
      socket = null;
      broadcastConnectionStatus(false);
      scheduleReconnect();
    };

    socket.onerror = () => {
      isConnecting = false;
      if (socket) {
        socket.close();
      }
    };
  } catch (err) {
    isConnecting = false;
    scheduleReconnect();
  }
}

function scheduleReconnect() {
  clearTimeout(reconnectTimer);
  reconnectTimer = setTimeout(connectWebSocket, 2000);
}

function broadcastConnectionStatus(connected) {
  try {
    chrome.runtime.sendMessage({ type: 'connection_status', connected });
  } catch { }
}

// Отправка состояния текущей активной вкладки на сервер ReLaitis
async function sendCurrentTabState() {
  if (!socket || socket.readyState !== WebSocket.OPEN) return;

  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    if (tabs.length > 0 && tabs[0].url) {
      const activeTab = tabs[0];
      socket.send(JSON.stringify({
        type: 'state_changed',
        url: activeTab.url,
        title: activeTab.title || ''
      }));
    }
  } catch (e) {
    console.error('[ReLaitis] Ошибка получения состояния вкладки:', e);
  }
}

// Обработка команд от ReLaitis
async function handleIncomingCommand(msg) {
  const { id, action } = msg;

  // 1. Управление навигацией по URL
  if (action === 'navigate') {
    try {
      const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
      if (tabs.length > 0) {
        await chrome.tabs.update(tabs[0].id, { url: msg.url });
        sendResponseToReLaitis({ id, success: true });
      } else {
        await chrome.tabs.create({ url: msg.url });
        sendResponseToReLaitis({ id, success: true });
      }
    } catch (e) {
      sendResponseToReLaitis({ id, success: false, error: e.message });
    }
    return;
  }

  // 2. Управление вкладками браузера
  if (action === 'tab_action') {
    try {
      const tabs = await chrome.tabs.query({ currentWindow: true });
      const activeTab = tabs.find(t => t.active);

      switch (msg.command) {
        case 'new':
          await chrome.tabs.create({});
          break;
        case 'close':
          if (activeTab) await chrome.tabs.remove(activeTab.id);
          break;
        case 'reload':
          if (activeTab) await chrome.tabs.reload(activeTab.id);
          break;
        case 'back':
          if (activeTab) await chrome.tabs.goBack(activeTab.id);
          break;
        case 'forward':
          if (activeTab) await chrome.tabs.goForward(activeTab.id);
          break;
        case 'next':
          if (activeTab && tabs.length > 1) {
            const currentIdx = tabs.indexOf(activeTab);
            const nextIdx = (currentIdx + 1) % tabs.length;
            await chrome.tabs.update(tabs[nextIdx].id, { active: true });
          }
          break;
        case 'prev':
          if (activeTab && tabs.length > 1) {
            const currentIdx = tabs.indexOf(activeTab);
            const prevIdx = (currentIdx - 1 + tabs.length) % tabs.length;
            await chrome.tabs.update(tabs[prevIdx].id, { active: true });
          }
          break;
      }
      sendResponseToReLaitis({ id, success: true });
    } catch (e) {
      sendResponseToReLaitis({ id, success: false, error: e.message });
    }
    return;
  }

  // 3. Действия на веб-странице (клик, скролл, показ бейджей, чтение текста, JS)
  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tabs || tabs.length === 0) {
      sendResponseToReLaitis({ id, success: false, error: 'No active tab found' });
      return;
    }

    const activeTab = tabs[0];
    chrome.tabs.sendMessage(activeTab.id, msg, (response) => {
      if (chrome.runtime.lastError) {
        sendResponseToReLaitis({
          id,
          success: false,
          error: chrome.runtime.lastError.message
        });
      } else {
        sendResponseToReLaitis({
          id,
          success: response ? response.success : true,
          data: response ? response.data : null,
          error: response ? response.error : null
        });
      }
    });
  } catch (err) {
    sendResponseToReLaitis({ id, success: false, error: err.message });
  }
}

function sendResponseToReLaitis(response) {
  if (socket && socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(response));
  }
}

// Отслеживание событий вкладок
chrome.tabs.onActivated.addListener(() => {
  sendCurrentTabState();
});

chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.status === 'complete' && tab.active) {
    sendCurrentTabState();
  }
});

// Сообщения от content-скриптов и всплывающего окна
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'state_changed') {
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(JSON.stringify(request));
    }
  } else if (request.type === 'get_connection_status') {
    sendResponse({ connected: socket !== null && socket.readyState === WebSocket.OPEN });
  } else if (request.type === 'reconnect') {
    connectWebSocket();
    sendResponse({ status: 'connecting' });
  }
  return true;
});

// Старт соединения при запуске расширения
connectWebSocket();
