(() => {
  let hintBadges = [];
  let hintElementsMap = new Map();

  // Отправка текущего состояния страницы в background service worker
  function reportPageState() {
    try {
      chrome.runtime.sendMessage({
        type: 'state_changed',
        url: window.location.href,
        title: document.title
      });
    } catch (e) {
      // Игнорируем ошибки при закрытии контекста
    }
  }

  // Очистка всех отображаемых номерных бейджей
  function clearHints() {
    hintBadges.forEach(b => b.remove());
    hintBadges = [];
    hintElementsMap.clear();
  }

  // Проверка видимости элемента на экране
  function isElementVisible(el) {
    const rect = el.getBoundingClientRect();
    if (rect.width <= 4 || rect.height <= 4) return false;

    const style = window.getComputedStyle(el);
    if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') {
      return false;
    }

    // Проверяем, находится ли элемент в пределах или близко к текущему экрану
    const windowHeight = window.innerHeight || document.documentElement.clientHeight;
    const windowWidth = window.innerWidth || document.documentElement.clientWidth;

    const inViewport = (
      rect.top >= -100 &&
      rect.left >= -50 &&
      rect.bottom <= windowHeight + 100 &&
      rect.right <= windowWidth + 50
    );

    return inViewport;
  }

  // Создание и размещение бейджей для всех интерактивных элементов
  function showHints() {
    clearHints();

    const selector = [
      'a[href]',
      'button',
      'input:not([type=hidden])',
      'select',
      'textarea',
      '[role="button"]',
      '[role="link"]',
      '[role="tab"]',
      '[role="checkbox"]',
      '[role="menuitem"]',
      '[tabindex="0"]',
      '[onclick]'
    ].join(', ');

    const candidates = Array.from(document.querySelectorAll(selector));
    let number = 1;

    for (const el of candidates) {
      if (isElementVisible(el)) {
        const rect = el.getBoundingClientRect();

        const badge = document.createElement('div');
        badge.className = 'relaitis-hint-badge';
        badge.textContent = number.toString();

        const pageX = rect.left + window.scrollX;
        const pageY = rect.top + window.scrollY;

        badge.style.left = `${pageX}px`;
        badge.style.top = `${pageY}px`;

        document.body.appendChild(badge);
        hintBadges.push(badge);
        hintElementsMap.set(number.toString(), el);

        number++;
        if (number > 150) break; // Защита от перегрузки интерфейса
      }
    }

    return hintElementsMap.size;
  }

  // Выполнение клика по элементу
  function clickTarget(target) {
    let el = null;
    const targetStr = (target || '').trim();

    // 1. Поиск по номеру бейджа
    if (hintElementsMap.has(targetStr)) {
      el = hintElementsMap.get(targetStr);
    }
    // 2. Поиск по CSS селектору
    else {
      try {
        el = document.querySelector(targetStr);
      } catch {
        el = null;
      }
    }

    // 3. Поиск по тексту ссылки или кнопки
    if (!el) {
      const lower = targetStr.toLowerCase();
      const clickable = Array.from(document.querySelectorAll('a, button, [role="button"], input[type="button"], input[type="submit"]'));
      el = clickable.find(e => {
        const text = (e.innerText || e.value || e.getAttribute('aria-label') || '').toLowerCase();
        return text.includes(lower);
      });
    }

    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'center' });
      el.classList.add('relaitis-clicked-element');
      setTimeout(() => el.classList.remove('relaitis-clicked-element'), 400);

      try {
        el.focus();
        el.click();
      } catch (e) {
        console.error('[ReLaitis] Click error:', e);
      }

      clearHints();
      return true;
    }

    return false;
  }

  // Обработка сообщений от background worker
  chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    switch (request.action) {
      case 'toggle_hints':
        if (request.show) {
          const count = showHints();
          sendResponse({ success: true, count: count });
        } else {
          clearHints();
          sendResponse({ success: true, count: 0 });
        }
        break;

      case 'click':
        const clicked = clickTarget(request.target);
        sendResponse({ success: clicked });
        break;

      case 'scroll':
        const amount = request.amount || 500;
        if (request.direction === 'down') {
          window.scrollBy({ top: amount, behavior: 'smooth' });
        } else if (request.direction === 'up') {
          window.scrollBy({ top: -amount, behavior: 'smooth' });
        } else if (request.direction === 'top') {
          window.scrollTo({ top: 0, behavior: 'smooth' });
        } else if (request.direction === 'bottom') {
          window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
        }
        if (hintBadges.length > 0) {
          setTimeout(showHints, 300);
        }
        sendResponse({ success: true });
        break;

      case 'get_text':
        let text = '';
        if (request.selector) {
          const el = document.querySelector(request.selector);
          text = el ? (el.innerText || el.textContent || '') : '';
        }
        sendResponse({ success: true, data: text });
        break;

      case 'script':
        try {
          const result = eval(request.code);
          sendResponse({ success: true, data: String(result) });
        } catch (err) {
          sendResponse({ success: false, error: err.message });
        }
        break;

      default:
        sendResponse({ success: false, error: `Unknown action: ${request.action}` });
        break;
    }

    return true; // Асинхронный ответ
  });

  // Отслеживаем изменения URL и загрузку
  window.addEventListener('load', reportPageState);
  window.addEventListener('focus', reportPageState);
  reportPageState();

  // Удаляем бейджи по нажатию Escape
  window.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && hintBadges.length > 0) {
      clearHints();
    }
  });
})();
