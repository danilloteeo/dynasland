/* Janela de configurações do Dynasland.
   O aplicativo envia o estado inteiro (SettingsViewModel) em JSON; esta página mostra e altera esse estado
   por caminhos, como "Privacy.ClipboardHistoryEnabled" ou "Modules[2].IsEnabled". Todo texto vindo do
   aplicativo entra por textContent, nunca como HTML. */
(function () {
  'use strict';

  var state = null;
  var page = null;
  var binders = [];
  var collector = binders;
  var web = window.chrome && window.chrome.webview;

  // ---------- conversa com o aplicativo ----------
  function post(message) { if (web) { web.postMessage(JSON.stringify(message)); } }

  function get(path) {
    return path.split('.').reduce(function (obj, seg) {
      if (obj == null) { return undefined; }
      var m = /^(\w+)(?:\[(\d+)\])?$/.exec(seg);
      var value = obj[m[1]];
      return m[2] !== undefined && value ? value[+m[2]] : value;
    }, state);
  }

  function setLocal(path, value) {
    var segs = path.split('.');
    var obj = state;
    for (var i = 0; i < segs.length - 1; i++) {
      var m = /^(\w+)(?:\[(\d+)\])?$/.exec(segs[i]);
      obj = obj[m[1]];
      if (m[2] !== undefined) { obj = obj[+m[2]]; }
      if (obj == null) { return; }
    }
    obj[segs[segs.length - 1]] = value;
  }

  function set(path, value) { setLocal(path, value); post({ type: 'set', path: path, value: value }); runBinders(); }
  function call(path, arg) { post({ type: 'call', path: path, arg: arg }); }
  function action(name) { post({ type: 'action', name: name }); }

  // ---------- montagem de elementos ----------
  function el(tag, cls, text) {
    var node = document.createElement(tag);
    if (cls) { node.className = cls; }
    if (text != null) { node.textContent = text; }
    return node;
  }
  function bind(fn) { collector.push(fn); fn(); }
  function runBinders() { binders.forEach(function (fn) { fn(); }); }
  function show(node, visible) { node.hidden = !visible; }

  var fmt = {
    pct: function (v) { return Math.round(v * 100) + '%'; },
    px: function (v) { return Math.round(v) + ' px'; },
    sec: function (v) { return Math.round(v) + ' s'; },
    x: function (v) { return String(Math.round(v * 100) / 100).replace('.', ',') + '×'; },
    int: function (v) { return String(Math.round(v)); },
    days: function (v) { return Math.round(v) + (v === 1 ? ' dia' : ' dias'); }
  };

  function group() {
    var g = el('div', 'group');
    Array.prototype.slice.call(arguments).forEach(function (c) { if (c) { g.append(c); } });
    return g;
  }
  function label(text, gap) { return el('div', gap ? 'label label-gap' : 'label', text); }
  function note(text) { return el('p', 'page-note', text); }
  function title(text) { return el('h1', null, text); }

  function boundText(cls, fn) {
    var node = el('p', cls);
    bind(function () { var t = fn(); node.textContent = t || ''; show(node, !!t); });
    return node;
  }

  function row(titleText, hint, control) {
    var r = el('div', 'setting-row');
    var t = el('span', 'setting-text');
    t.append(el('span', null, titleText));
    if (hint) { t.append(el('small', null, hint)); }
    r.append(t);
    if (control) { r.append(control); }
    return r;
  }

  function sw(path, disabledFn) {
    var l = el('label', 'switch');
    var input = document.createElement('input');
    input.type = 'checkbox';
    l.append(input, document.createElement('span'));
    bind(function () { input.checked = !!get(path); input.disabled = !!(disabledFn && disabledFn()); });
    input.addEventListener('change', function () { set(path, input.checked); });
    return l;
  }

  function seg(path, options, two) {
    var wrap = el('div', 'segmented' + (two ? ' two' : ''));
    wrap.setAttribute('role', 'radiogroup');
    var buttons = options.map(function (o) {
      var b = el('button', null, o[1]);
      b.setAttribute('role', 'radio');
      b.addEventListener('click', function () { set(path, o[0]); });
      wrap.append(b);
      return b;
    });
    bind(function () {
      var current = get(path);
      var chosen = 0;
      buttons.forEach(function (b, i) {
        var on = options[i][0] === current;
        if (on) { chosen = i; }
        b.setAttribute('aria-checked', String(on));
      });
      wrap.style.setProperty('--seg-i', String(chosen));
    });
    return wrap;
  }

  function stepper(path, min, max, step, format) {
    var wrap = el('div', 'stepper');
    var minus = el('button', null, '−');
    var out = document.createElement('output');
    var plus = el('button', null, '+');
    minus.setAttribute('aria-label', 'Diminuir');
    plus.setAttribute('aria-label', 'Aumentar');
    wrap.append(minus, out, plus);
    function nudge(dir) {
      var v = Number(get(path)) || 0;
      var next = Math.min(max, Math.max(min, Math.round((v + dir * step) / step) * step));
      set(path, Math.round(next * 1000) / 1000);
    }
    minus.addEventListener('click', function () { nudge(-1); });
    plus.addEventListener('click', function () { nudge(1); });
    bind(function () {
      var v = Number(get(path)) || 0;
      out.textContent = format(v);
      minus.disabled = v <= min;
      plus.disabled = v >= max;
    });
    return wrap;
  }

  function stepRow(text, path, min, max, step, format) {
    var r = el('div', 'stepper-row');
    r.append(el('span', null, text), stepper(path, min, max, step, format));
    return r;
  }

  function button(text, onClick, cls) {
    var b = el('button', 'btn' + (cls ? ' ' + cls : ''), text);
    b.addEventListener('click', onClick);
    return b;
  }

  /** Botão que pede um segundo clique em 3 segundos antes de agir (apagar, restaurar). */
  function confirmButton(text, onConfirm, cls) {
    var timer = null;
    var b = button(text, function () {
      if (timer) { clearTimeout(timer); timer = null; b.textContent = text; onConfirm(); return; }
      b.textContent = 'Clique de novo para confirmar';
      timer = setTimeout(function () { timer = null; b.textContent = text; }, 3000);
    }, cls);
    return b;
  }

  function btnRow() {
    var r = el('div', 'btn-row');
    Array.prototype.slice.call(arguments).forEach(function (c) { if (c) { r.append(c); } });
    return r;
  }

  function textField(path, placeholder, onEnter) {
    var f = el('input', 'field');
    f.type = 'text';
    f.placeholder = placeholder || '';
    f.spellcheck = false;
    f.addEventListener('input', function () { set(path, f.value); });
    f.addEventListener('keydown', function (e) { if (e.key === 'Enter' && onEnter) { onEnter(); } });
    bind(function () { if (document.activeElement !== f) { f.value = get(path) || ''; } });
    return f;
  }

  /** Lista que se refaz só quando muda a quantidade ou a identidade dos itens. */
  function dynList(path, container, keyFn, build) {
    var signature = null;
    var scope = [];
    bind(function () {
      var items = get(path) || [];
      var sig = items.length + ':' + items.map(keyFn).join('|');
      if (sig !== signature) {
        signature = sig;
        scope = [];
        var saved = collector;
        collector = scope;
        container.textContent = '';
        items.forEach(function (_, i) { container.append(build(i, path + '[' + i + ']')); });
        collector = saved;
      }
      scope.forEach(function (fn) { fn(); });
    });
    return container;
  }

  // ---------- seções ----------
  var ICONS = {
    General: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>',
    Appearance: '<path d="M12 3a9 9 0 1 0 0 18c1.1 0 1.7-.9 1.4-1.8l-.4-1.1a1.6 1.6 0 0 1 1.5-2.1H17a4 4 0 0 0 4-4C21 6.7 17 3 12 3z"/><path d="M7.5 11.5h.01M10 7.5h.01M14.5 7.5h.01M17 11h.01" stroke-width="2.4"/>',
    Animations: '<path d="M3 12h4l3-8 4 16 3-8h4"/>',
    Behavior: '<path d="M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6"/>',
    Monitors: '<rect x="3" y="8" width="18" height="8" rx="4"/>',
    Modules: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
    Notifications: '<path d="M6 16v-5a6 6 0 0 1 12 0v5l2 2H4z"/><path d="M10 21h4"/>',
    Privacy: '<rect x="5" y="11" width="14" height="10" rx="2"/><path d="M8 11V7a4 4 0 0 1 8 0v4"/>',
    Storage: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 12c0 1.7 3.6 3 8 3s8-1.3 8-3"/>',
    About: '<circle cx="12" cy="12" r="9"/><path d="M12 11v5" stroke-width="2"/><path d="M12 7.8v.01" stroke-width="2"/>'
  };

  var PAGES = [
    ['General', 'Geral'], ['Appearance', 'Aparência'], ['Animations', 'Animações'], ['Behavior', 'Comportamento'],
    ['Monitors', 'Posição'], ['Modules', 'Módulos'], ['Notifications', 'Notificações'], ['Privacy', 'Privacidade'],
    ['Storage', 'Armazenamento'], ['About', 'Sobre']
  ];

  var builders = {
    General: function () {
      var results = el('div', 'pick-list');
      dynList('Weather.Results', results, function (r) { return r.Name; }, function (i, p) {
        var b = el('button');
        b.textContent = '';
        bind(function () { b.textContent = get(p + '.Name') || ''; });
        b.addEventListener('click', function () { call(p + '.Select'); });
        return b;
      });
      var search = button('Buscar', function () { call('Weather.SearchCommand'); });
      var clear = button('Desligar clima', function () { call('Weather.ClearCommand'); });
      bind(function () { show(clear, !!get('Weather.HasLocation')); });

      var connect = button('Conectar', function () { call('Spotify.ConnectCommand'); }, 'primary');
      var disconnect = button('Desconectar', function () { call('Spotify.DisconnectCommand'); });
      bind(function () {
        show(connect, !get('Spotify.IsConnected'));
        connect.disabled = !get('Spotify.CanConnect');
        show(disconnect, !!get('Spotify.IsConnected'));
      });

      var wrap = el('section', 'page');
      wrap.append(
        title('Geral'),
        group(
          row('Iniciar com o Windows', null, sw('StartWithWindows')),
          row('Iniciar minimizado', 'Abre o Dynasland sem mostrar janelas.', sw('StartHidden')),
          row('Mostrar a ilha', null, sw('IsIslandVisible'))),
        label('Clima'),
        group(
          boundText('value-note', function () { var c = get('Weather.CurrentLocation'); return c ? 'Cidade escolhida: ' + c : 'Nenhuma cidade escolhida. O clima só é buscado depois que você escolher uma.'; }),
          (function () { var r = el('div', 'field-row'); r.append(textField('Weather.Query', 'Buscar cidade', function () { call('Weather.SearchCommand'); }), search); return r; })(),
          boundText('value-note', function () { return get('Weather.SearchMessage'); }),
          results,
          btnRow(clear)),
        label('Spotify'),
        group(
          boundText('value-note', function () { return get('Spotify.StatusText'); }),
          (function () { var r = el('div', 'field-row'); r.append(textField('Spotify.ClientId', 'Client ID do aplicativo no Spotify')); return r; })(),
          boundText('value-note', function () { return get('Spotify.Message'); }),
          btnRow(connect, disconnect,
            button('Abrir painel do Spotify', function () { call('Spotify.OpenDashboardCommand'); }),
            button('Copiar Redirect URI', function () { call('Spotify.CopyRedirectUriCommand'); }))));
      return wrap;
    },

    Appearance: function () {
      var swatches = el('div', 'swatches');
      dynList('AccentOptions', swatches, function (a) { return a.Hex; }, function (i, p) {
        var b = el('button', 'swatch');
        b.setAttribute('role', 'radio');
        b.style.background = get(p + '.Hex');
        b.title = get(p + '.Name') || '';
        b.setAttribute('aria-label', get(p + '.Name') || '');
        b.addEventListener('click', function () { set(p + '.IsSelected', true); });
        bind(function () { b.setAttribute('aria-checked', String(!!get(p + '.IsSelected'))); });
        return b;
      });

      var wrap = el('section', 'page');
      wrap.append(
        title('Aparência'),
        group(label('Tema das janelas'), seg('Theme', [['System', 'Automático'], ['Dark', 'Escuro'], ['Light', 'Claro']])),
        group(label('Cor de destaque'), swatches),
        note('A ilha tem sempre o tamanho e o preto do Volum: 250 × 32 px fechada e 440 px de largura aberta.'));
      return wrap;
    },

    Animations: function () {
      var wrap = el('section', 'page');
      wrap.append(
        title('Animações'),
        group(row('Animações', 'Desligadas, a ilha muda de tamanho sem movimento.', sw('AnimationsEnabled'))),
        group(
          stepRow('Velocidade', 'AnimationSpeed', 0.5, 2, 0.25, fmt.x),
          stepRow('Intensidade da mola', 'AnimationIntensity', 0, 2, 0.25, fmt.pct),
          stepRow('Tempo de permanência expandida', 'ExpandedDurationSeconds', 2, 20, 1, fmt.sec)));
      return wrap;
    },

    Behavior: function () {
      var unpin = button('Desafixar', function () { call('Behavior.UnpinCommand'); });
      bind(function () { show(unpin, !!get('Behavior.PinnedModuleName')); });
      var wrap = el('section', 'page');
      wrap.append(
        title('Comportamento'),
        group(row('Recolher ao clicar fora', 'Fecha o painel aberto ao clicar em outro lugar.', sw('Behavior.CollapseOnClickOutside'))),
        group(label('Duplo clique na ilha'), seg('Behavior.DoubleClickAction',
          [['None', 'Nenhuma ação'], ['PlayPauseMedia', 'Reproduzir/pausar mídia'], ['OpenSettings', 'Abrir configurações']])),
        group(row('Ocultar em tela cheia', 'Esconde a ilha com jogos e vídeos em tela cheia.', sw('HideInFullscreen'))),
        label('Módulo fixado'),
        group(boundText('value-note', function () { var n = get('Behavior.PinnedModuleName'); return n ? 'Fixado: ' + n : 'Nenhum módulo fixado.'; }), btnRow(unpin)));
      return wrap;
    },

    Monitors: function () {
      var monitors = el('div', 'pick-list');
      dynList('MonitorOptions', monitors, function (m) { return m.Name; }, function (i, p) {
        var b = el('button');
        b.setAttribute('role', 'radio');
        var name = document.createTextNode(get(p + '.Name') || '');
        var small = el('small', null, get(p + '.Detail') || '');
        b.append(name, small);
        b.addEventListener('click', function () { set(p + '.IsSelected', true); });
        bind(function () { b.setAttribute('aria-checked', String(!!get(p + '.IsSelected'))); });
        return b;
      });

      var wrap = el('section', 'page');
      wrap.append(
        title('Posição'),
        group(
          label('Borda da tela'), seg('Edge', [['Bottom', 'Embaixo'], ['Top', 'Em cima']], true),
          label('Posição na borda', true), seg('Position', [['Left', 'Esquerda'], ['Center', 'Centro'], ['Right', 'Direita']]),
          label('Formato', true), seg('Shape', [['Float', 'Flutuante'], ['Notch', 'Encaixada']], true)),
        group(stepRow('Distância da borda', 'TopMargin', 0, 60, 4, fmt.px)),
        label('Exibir no monitor'),
        group(monitors));
      return wrap;
    },

    Modules: function () {
      var list = el('div');
      dynList('Modules', list, function (m) { return m.Id; }, function (i, p) {
        var head = el('div', 'mod-head');
        var name = el('span', 'mod-name');
        var nameText = document.createTextNode(get(p + '.Name') || '');
        var state = el('span', 'mod-state');
        name.append(nameText, state);
        head.append(name, sw(p + '.IsEnabled', function () { return !get(p + '.CanToggle'); }));
        var desc = el('p', 'mod-desc', get(p + '.Description') || '');
        var reason = el('p', 'mod-desc');
        var opts = el('div', 'mod-opts');
        opts.append(row('Mostrar na dock', 'Um ícone deste módulo na dock.', sw(p + '.ShowInDock')));
        bind(function () {
          state.textContent = get(p + '.StatusText') || '';
          state.className = 'mod-state' + (get(p + '.Status') === 'Failed' || get(p + '.Status') === 'Unavailable' ? ' bad' : '');
          reason.textContent = get(p + '.Reason') || '';
          show(reason, !!get(p + '.HasReason'));
          show(opts, !!get(p + '.ShowDockRow') || !!get(p + '.ShowOptions'));
        });
        var optList = el('div');
        dynList(p + '.Options', optList, function (o) { return o.Label; }, function (j, q) {
          if (get(q + '.IsToggle')) {
            return row(get(q + '.Label'), get(q + '.Description'), sw(q + '.IsOn'));
          }
          var r = el('div', 'stepper-row');
          var t = el('span');
          t.append(document.createTextNode(get(q + '.Label') || ''));
          var unit = (get(q + '.ValueText') || '').replace(/^[\d.,\-]+\s*/, '');
          r.append(t, stepper(q + '.Value', get(q + '.Minimum'), get(q + '.Maximum'), get(q + '.Step') || 1,
            function (v) { return (Math.round(v * 100) / 100).toString().replace('.', ',') + (unit ? ' ' + unit : ''); }));
          return r;
        });
        bind(function () { show(optList, !!get(p + '.ShowOptions')); });
        opts.append(optList);
        var g = group(head, desc, reason, opts);
        return g;
      });

      var wrap = el('section', 'page');
      wrap.append(title('Módulos'), boundText('page-note', function () { return get('ModulesSummary'); }), list);
      return wrap;
    },

    Notifications: function () {
      var apps = el('div');
      dynList('NotificationApps', apps, function (a) { return a.AppId; }, function (i, p) {
        var head = el('div', 'mod-head');
        head.append(el('span', 'mod-name', get(p + '.Name') || ''), sw(p + '.Shows'));
        var more = el('div', 'mod-opts');
        more.append(
          row('Só no histórico', 'Não aparece na ilha, mas fica guardado.', sw(p + '.HistoryOnly')),
          row('Prioridade alta', null, sw(p + '.HighPriority')),
          row('Duração própria', null, sw(p + '.HasCustomDuration')));
        var dur = stepRow('Duração deste aplicativo', p + '.DurationSeconds', 2, 30, 1, fmt.sec);
        more.append(dur);
        bind(function () { show(dur, !!get(p + '.HasCustomDuration')); show(more, !!get(p + '.Shows')); });
        return group(head, more);
      });

      var wrap = el('section', 'page');
      wrap.append(
        title('Notificações'),
        group(label('Aplicativos permitidos'), seg('NotificationMode', [['AllExceptBlocked', 'Todos, exceto os bloqueados'], ['OnlyAllowed', 'Somente os permitidos']], true)),
        group(
          stepRow('Duração dos avisos', 'NotificationDuration', 2, 20, 1, fmt.sec),
          row('Prioridade alta', 'Avisos importantes ficam mais tempo e podem interromper.', sw('NotificationsCanInterrupt'))),
        label('Aplicativos'),
        boundText('page-note', function () { return get('HasNotificationApps') ? '' : 'Os aplicativos aparecem aqui depois da primeira notificação deles.'; }),
        apps);
      return wrap;
    },

    Privacy: function () {
      var wrap = el('section', 'page');
      wrap.append(
        title('Privacidade'),
        note('Tudo fica neste computador. Nada é enviado a servidores, exceto a consulta de versão ao GitHub (se ligada em Sobre), a previsão do tempo (se você escolher uma cidade) e as cotações e o placar da NBA, buscados só enquanto o painel está aberto.'),
        label('Área de transferência'),
        group(
          row('Guardar histórico', 'Guarda o que você copia para colar de novo.', sw('Privacy.ClipboardHistoryEnabled')),
          row('Incluir imagens', null, sw('Privacy.ClipboardStoreImages')),
          btnRow(confirmButton('Apagar histórico da área de transferência', function () { call('Privacy.ClearClipboardCommand'); }, 'danger'))),
        label('Notificações'),
        group(
          row('Guardar histórico de notificações', null, sw('Privacy.NotificationHistoryEnabled')),
          btnRow(confirmButton('Apagar histórico de notificações', function () { call('Privacy.ClearNotificationsCommand'); }, 'danger'))),
        boundText('page-note', function () { return get('Privacy.LastAction'); }));
      return wrap;
    },

    Storage: function () {
      var wrap = el('section', 'page');
      wrap.append(
        title('Armazenamento'),
        label('Banco de dados'),
        group(
          boundText('value-note', function () { return 'Tamanho: ' + (get('Storage.DatabaseSize') || '—'); }),
          btnRow(button('Compactar', function () { call('Storage.CompactCommand'); }))),
        label('Retenção dos históricos'),
        group(
          stepRow('Itens da área de transferência', 'Storage.ClipboardMaxItems', 20, 1000, 10, fmt.int),
          stepRow('Notificações', 'Storage.NotificationHistoryDays', 1, 90, 1, fmt.days)),
        label('Cópia de segurança'),
        group(btnRow(button('Exportar configurações…', function () { action('export'); }), button('Importar configurações…', function () { action('import'); }))),
        label('Pastas'),
        group(
          boundText('value-note', function () { return get('Storage.DatabasePath'); }),
          btnRow(button('Abrir pasta de dados', function () { call('Storage.OpenDataFolderCommand'); }),
            button('Abrir pasta do diagnóstico', function () { call('Storage.OpenLogsFolderCommand'); }))),
        boundText('page-note', function () { return get('Storage.LastAction'); }));
      return wrap;
    },

    About: function () {
      var head = el('div', 'about-head');
      head.innerHTML = '<svg viewBox="0 0 32 32" aria-hidden="true"><circle cx="16" cy="16" r="15.5" fill="#090909" stroke="#fff" stroke-opacity=".27"/><rect x="6" y="12.5" width="20" height="7" rx="3.5" fill="#fff"/></svg>';
      var who = el('div');
      who.append(el('div', 'about-name', 'Dynasland'), boundText('about-version', function () { return 'Versão ' + get('Version'); }));
      head.append(who);

      var install = button('Atualizar agora', function () { call('Updates.InstallCommand'); }, 'primary');
      var bar = el('div', 'progress');
      var fill = document.createElement('i');
      bar.append(fill);
      var checkNow = button('Verificar agora', function () { call('CheckUpdatesNowCommand'); });
      bind(function () {
        var stage = get('Updates.Stage');
        show(install, stage === 'Available');
        show(bar, stage === 'Downloading');
        fill.style.width = Math.round((Number(get('Updates.Progress')) || 0) * 100) + '%';
        checkNow.disabled = !!get('IsCheckingUpdates');
      });

      var wrap = el('section', 'page');
      wrap.append(
        title('Sobre'),
        group(head, el('p', 'about-text', 'Uma ilha para o Windows: música, volume, água, avisos e muito mais num só lugar. Visual das janelas baseado no Volum (licença MIT).')),
        label('Atualizações'),
        group(
          row('Verificar atualizações automaticamente', null, sw('CheckForUpdates')),
          boundText('value-note', function () {
            var stage = get('Updates.Stage');
            if (stage === 'Available') { return 'Versão ' + get('Updates.Version') + ' disponível.'; }
            if (stage === 'Failed') { return get('Updates.Error') || 'A atualização falhou.'; }
            if (stage === 'Installing') { return 'Instalando…'; }
            return get('UpdateStatusText');
          }),
          bar,
          btnRow(checkNow, install)),
        label('Restaurar'),
        group(row('Restaurar configurações padrão', 'Aparência, animações, comportamento, módulos e notificações voltam ao original.',
          button('Restaurar…', function () { action('restore'); }))),
        label('Informações'),
        group(row('Idioma', null, el('span', 'value-note', 'Português (Brasil)'))));
      return wrap;
    }
  };

  // ---------- navegação e estado ----------
  var nav = document.getElementById('nav');
  var pages = document.getElementById('pages');

  function buildNav() {
    var brand = el('div', 'nav-brand');
    brand.innerHTML = '<svg class="logo-mark" viewBox="0 0 32 32" aria-hidden="true"><circle cx="16" cy="16" r="15.5" fill="#090909" stroke="#fff" stroke-opacity=".27"/><rect x="6" y="12.5" width="20" height="7" rx="3.5" fill="#fff"/></svg>';
    brand.append(el('span', null, 'Configurações'));
    nav.append(brand);
    PAGES.forEach(function (p) {
      var b = el('button', 'nav-item');
      b.dataset.page = p[0];
      b.innerHTML = '<svg class="ico" viewBox="0 0 24 24" aria-hidden="true">' + ICONS[p[0]] + '</svg>';
      b.append(el('span', null, p[1]));
      b.addEventListener('click', function () { openPage(p[0], true); });
      nav.append(b);
    });
  }

  function openPage(name, fromUser) {
    page = name;
    Array.prototype.forEach.call(nav.querySelectorAll('.nav-item'), function (b) {
      if (b.dataset.page === name) { b.setAttribute('aria-current', 'page'); } else { b.removeAttribute('aria-current'); }
    });
    binders = [];
    collector = binders;
    pages.textContent = '';
    pages.append(builders[name]());
    pages.scrollTop = 0;
    if (fromUser && state && state.Section !== name) { set('Section', name); }
  }

  function applyScheme() {
    var theme = state && state.Theme;
    var dark = theme === 'Dark' || (theme !== 'Light' && !(window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches));
    document.documentElement.dataset.scheme = dark ? 'dark' : 'light';
  }

  function onState(data) {
    var previousSection = state && state.Section;
    state = data;
    applyScheme();
    if (page === null || (data.Section !== previousSection && data.Section !== page)) {
      openPage(builders[data.Section] ? data.Section : 'General', false);
    } else {
      runBinders();
    }
  }

  buildNav();
  if (web) {
    web.addEventListener('message', function (e) {
      var message = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
      if (message.type === 'state') { onState(message.data); }
    });
    post({ type: 'ready' });
  }
})();
