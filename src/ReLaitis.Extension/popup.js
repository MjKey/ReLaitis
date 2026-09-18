document.addEventListener('DOMContentLoaded', async () => {
  const statusBadge = document.getElementById('statusBadge');
  const statusText = document.getElementById('statusText');
  const tabTitle = document.getElementById('tabTitle');
  const tabUrl = document.getElementById('tabUrl');
  const showHintsBtn = document.getElementById('showHintsBtn');
  const hideHintsBtn = document.getElementById('hideHintsBtn');
  const reconnectBtn = document.getElementById('reconnectBtn');

  // Получение активной вкладки
  const [activeTab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (activeTab) {
    tabTitle.textContent = activeTab.title || 'Без названия';
    tabUrl.textContent = activeTab.url || '';
  }

  // Проверка статуса подключения
  chrome.runtime.sendMessage({ type: 'get_connection_status' }, (resp) => {
    updateStatus(resp && resp.connected);
  });

  // Слушаем изменения статуса
  chrome.runtime.onMessage.addListener((msg) => {
    if (msg.type === 'connection_status') {
      updateStatus(msg.connected);
    }
  });

  function updateStatus(isConnected) {
    if (isConnected) {
      statusBadge.classList.add('connected');
      statusText.textContent = 'В сети (11337)';
    } else {
      statusBadge.classList.remove('connected');
      statusText.textContent = 'Не подключен';
    }
  }

  showHintsBtn.addEventListener('click', () => {
    if (activeTab?.id) {
      chrome.tabs.sendMessage(activeTab.id, { action: 'toggle_hints', show: true });
    }
  });

  hideHintsBtn.addEventListener('click', () => {
    if (activeTab?.id) {
      chrome.tabs.sendMessage(activeTab.id, { action: 'toggle_hints', show: false });
    }
  });

  reconnectBtn.addEventListener('click', () => {
    statusText.textContent = 'Подключение...';
    chrome.runtime.sendMessage({ type: 'reconnect' });
  });
});
