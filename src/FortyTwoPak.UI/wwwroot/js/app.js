/* 42pak – UI Controller */
(function () {
  'use strict';

  const B = () => window.chrome.webview.hostObjects.sync.bridge;
  const $ = (s, ctx) => (ctx || document).querySelector(s);
  const $$ = (s, ctx) => (ctx || document).querySelectorAll(s);

  // ── Navigation ──
  const viewTitles = {
    create: 'Create Archive',
    manage: 'Manage Archive',
    convert: 'Convert EIX/EPK',
    settings: 'Settings',
    help: 'Help'
  };

  document.addEventListener('DOMContentLoaded', () => {
    $$('.nav-item[data-view]').forEach(n => n.addEventListener('click', () => switchView(n.dataset.view)));
    bindCreate();
    bindManage();
    bindConvert();
    bindSettings();
    bindKeyboard();
    bindDragDrop();
    loadPrefs();
    loadRecentFiles();
  });

  function switchView(id) {
    $$('.nav-item[data-view]').forEach(n => n.classList.toggle('active', n.dataset.view === id));
    $$('.view').forEach(v => v.classList.toggle('active', v.id === 'v-' + id));
    $('#view-title').textContent = viewTitles[id] || id;
    $('#search-wrap').style.display = id === 'manage' ? '' : 'none';
  }

  // ── TOAST ──
  function toast(msg, type) {
    type = type || 'info';
    var icons = { success: 'fa-circle-check', error: 'fa-circle-xmark', info: 'fa-circle-info' };
    var el = document.createElement('div');
    el.className = 'toast toast-' + type;
    el.innerHTML = '<i class="fa-solid ' + (icons[type] || icons.info) + '"></i>' +
      '<span class="toast-msg">' + escapeHtml(msg) + '</span>' +
      '<button class="toast-close" onclick="this.parentElement.classList.add(\'toast-out\')">&times;</button>';
    $('#toast-container').appendChild(el);
    setTimeout(function () {
      el.classList.add('toast-out');
      setTimeout(function () { el.remove(); }, 250);
    }, 4000);
  }

  // ── CREATE ──
  function bindCreate() {
    $('#c-enc').addEventListener('change', function () {
      $('#c-pass-fields').style.display = $('#c-enc').checked ? '' : 'none';
    });
    $('#c-comp').addEventListener('input', function () {
      $('#c-comp-val').textContent = $('#c-comp').value;
    });
  }

  window.pickSourceFolder = function () {
    var path = B().PickFolder();
    if (path) $('#c-src').value = path;
  };

  window.pickOutputFile = function () {
    var path = B().PickSaveFile('VPK Archive (*.vpk)|*.vpk', '.vpk');
    if (path) $('#c-out').value = path;
  };

  window.buildVpk = function () {
    var src = $('#c-src').value.trim();
    var out = $('#c-out').value.trim();
    if (!src || !out) return modal('Missing Fields', 'Select both a source folder and output path.');

    var enc = $('#c-enc').checked;
    if (enc) {
      var p1 = $('#c-pass').value;
      var p2 = $('#c-pass2').value;
      if (p1.length < 8) return modal('Weak Passphrase', 'Passphrase must be at least 8 characters.');
      if (p1 !== p2) return modal('Mismatch', 'Passphrases do not match.');
    }

    var opts = JSON.stringify({
      passphrase: enc ? $('#c-pass').value : null,
      compressionLevel: parseInt($('#c-comp').value, 10),
      compressionAlgorithm: $('#c-algo').value,
      mangleFileNames: $('#c-mangle').checked,
      author: $('#c-author').value.trim() || null,
      comment: $('#c-comment').value.trim() || null
    });

    var btn = $('#btn-build');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Building...';
    showProgress();

    setTimeout(function () {
      try {
        var r = JSON.parse(B().BuildVpk(src, out, opts));
        hideProgress();
        if (r.success) {
          toast(r.message, 'success');
        } else {
          modal('Build Failed', r.message);
        }
      } catch (e) {
        hideProgress();
        modal('Error', e.message || String(e));
      } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-hammer"></i> Build Archive';
      }
    }, 50);
  };

  function showProgress() {
    var el = $('#c-progress');
    el.style.display = '';
    $('#c-progress-fill').style.width = '0%';
    $('#c-progress-txt').textContent = '';
    animateProgress();
  }

  function hideProgress() {
    $('#c-progress-fill').style.width = '100%';
    $('#c-progress-txt').textContent = '100%';
    clearInterval(_progTimer);
    setTimeout(function () { $('#c-progress').style.display = 'none'; }, 400);
  }

  var _progTimer = null;
  function animateProgress() {
    var pct = 0;
    clearInterval(_progTimer);
    _progTimer = setInterval(function () {
      pct = Math.min(pct + Math.random() * 8, 90);
      $('#c-progress-fill').style.width = pct + '%';
      $('#c-progress-txt').textContent = Math.round(pct) + '%';
    }, 200);
  }

  // ── MANAGE ──
  var _allEntries = [];
  var _openPassphrase = '';

  function bindManage() {
    $('#search-input').addEventListener('input', filterEntries);
  }

  window.openVpkFile = function () {
    var path = B().PickFile('VPK Archive (*.vpk)|*.vpk');
    if (!path) return;
    var pass = prompt('Enter passphrase (leave blank if none):') || '';
    openVpkByPath(path, pass);
  };

  function openVpkByPath(path, pass) {
    try {
      var r = JSON.parse(B().OpenVpk(path, pass));
      if (!r.success) return modal('Open Failed', r.message);
      _openPassphrase = pass;
      displayArchive(r);
      loadRecentFiles();
      switchView('manage');
      toast('Opened ' + path.split('\\').pop(), 'success');
    } catch (e) {
      modal('Error', e.message || String(e));
    }
  }

  function displayArchive(data) {
    var h = data.header;
    var meta = $('#archive-meta');
    meta.style.display = '';
    meta.innerHTML = [
      mp('Version', h.Version),
      mp('Files', h.EntryCount),
      mp('Encrypted', h.IsEncrypted ? 'Yes' : 'No'),
      mp('Algorithm', h.CompressionAlgorithm || 'LZ4'),
      mp('Level', h.CompressionLevel),
      mp('Mangled', h.FileNamesMangled ? 'Yes' : 'No'),
      mp('Author', h.Author || '—'),
      mp('Created', new Date(h.CreatedAt).toLocaleDateString())
    ].join('');

    _allEntries = data.entries || [];
    renderEntries(_allEntries);
    loadArchiveStats();
    $('#recent-panel').style.display = 'none';
    $('#btn-extract').disabled = false;
    $('#btn-validate').disabled = false;
  }

  function loadArchiveStats() {
    try {
      var r = JSON.parse(B().GetArchiveStats());
      if (!r.success) return;
      var bar = $('#archive-stats');
      bar.style.display = '';
      bar.innerHTML =
        stat('Total Files', r.totalFiles) +
        stat('Original', formatSize(r.totalOriginal)) +
        stat('Compressed', formatSize(r.totalStored)) +
        stat('Ratio', r.overallRatio + '%') +
        stat('Archive Size', formatSize(r.archiveSize));
    } catch (_) {}
  }

  function stat(label, val) {
    return '<span class="stat"><span class="stat-label">' + label + '</span> <span class="stat-val">' + escapeHtml(String(val)) + '</span></span>';
  }

  function mp(label, val) {
    return '<span><span class="m-label">' + label + '</span><span class="m-val">' + escapeHtml(String(val)) + '</span></span>';
  }

  function renderEntries(entries) {
    var list = $('#file-list');
    if (!entries.length) {
      list.innerHTML = '<div class="empty-msg"><i class="fa-solid fa-box-open"></i><p>No files in archive.</p></div>';
      return;
    }
    list.innerHTML = entries.map(function (e, idx) {
      var tags = [];
      if (e.IsEncrypted) tags.push('<span class="f-tag enc">ENC</span>');
      var algoTag = e.IsCompressed ? (e.compressionAlgorithm || 'LZ4') : '';
      if (algoTag) tags.push('<span class="f-tag lz4">' + escapeHtml(algoTag) + '</span>');
      var ratio = e.ratio != null ? e.ratio : (e.OriginalSize > 0 ? Math.round((1 - e.StoredSize / e.OriginalSize) * 1000) / 10 : 0);
      return '<div class="file-row">' +
        '<span class="f-icon"><i class="' + getFileIcon(e.FileName) + '"></i></span>' +
        '<span class="f-name" title="' + escapeHtml(e.FileName) + '">' + escapeHtml(e.FileName) + '</span>' +
        tags.join('') +
        '<span class="f-ratio">' + ratio + '%</span>' +
        '<span class="f-size">' + formatSize(e.OriginalSize) + '</span>' +
        '<span class="f-actions"><button onclick="extractSingleFile(\'' + escapeAttr(e.FileName) + '\')"><i class="fa-solid fa-download"></i> Extract</button></span>' +
        '</div>';
    }).join('');
  }

  function filterEntries() {
    var q = $('#search-input').value.toLowerCase();
    if (!q) return renderEntries(_allEntries);
    renderEntries(_allEntries.filter(function (e) { return e.FileName.toLowerCase().indexOf(q) >= 0; }));
  }

  window.extractSingleFile = function (fileName) {
    var dir = B().PickFolder();
    if (!dir) return;
    try {
      var r = JSON.parse(B().ExtractSingleFile(fileName, dir, _openPassphrase));
      if (r.success) toast('Extracted ' + fileName, 'success');
      else modal('Error', r.message);
    } catch (e) {
      modal('Error', e.message || String(e));
    }
  };

  window.extractAllFiles = function () {
    var dir = B().PickFolder();
    if (!dir) return;
    var pass = _openPassphrase || (prompt('Passphrase (blank if none):') || '');
    try {
      var r = JSON.parse(B().ExtractAll(dir, pass));
      if (r.success) toast(r.message, 'success');
      else modal('Error', r.message);
    } catch (e) {
      modal('Error', e.message || String(e));
    }
  };

  window.validateArchive = function () {
    var pass = _openPassphrase || (prompt('Passphrase (blank if none):') || '');
    try {
      var r = JSON.parse(B().ValidateVpk(pass));
      if (!r.success) return modal('Error', r.message);
      if (r.isValid) {
        toast('Archive valid — ' + r.validFiles + ' files OK.', 'success');
      } else {
        modal('Integrity Issues', 'Errors found:\n' + (r.errors || []).join('\n'));
      }
    } catch (e) {
      modal('Error', e.message || String(e));
    }
  };

  // ── RECENT FILES ──
  function loadRecentFiles() {
    try {
      var r = JSON.parse(B().GetRecentFiles());
      var list = $('#recent-list');
      if (!r.success || !r.files || !r.files.length) {
        list.innerHTML = '<div class="empty-msg"><i class="fa-solid fa-clock-rotate-left"></i><p>No recent files.</p></div>';
        return;
      }
      list.innerHTML = r.files.map(function (f) {
        var name = f.split('\\').pop();
        return '<div class="recent-item" onclick="openRecentFile(\'' + escapeAttr(f) + '\')">' +
          '<i class="fa-solid fa-box-archive"></i>' +
          '<span class="recent-name">' + escapeHtml(name) + '</span>' +
          '<span class="recent-path" title="' + escapeHtml(f) + '">' + escapeHtml(f) + '</span>' +
          '</div>';
      }).join('');
    } catch (_) {}
  }

  window.openRecentFile = function (path) {
    var pass = prompt('Enter passphrase (leave blank if none):') || '';
    openVpkByPath(path, pass);
  };

  // ── CONVERT ──
  function bindConvert() {
    $('#cv-enc').addEventListener('change', function () {
      $('#cv-pass-row').style.display = $('#cv-enc').checked ? '' : 'none';
    });
  }

  window.pickEixFile = function () {
    var path = B().PickFile('EIX Index (*.eix)|*.eix');
    if (!path) return;
    $('#cv-eix').value = path;
    loadEixPreview(path);
  };

  function loadEixPreview(eixPath) {
    try {
      var r = JSON.parse(B().ReadEixListing(eixPath));
      if (!r.success) return;
      var grid = $('#eix-grid');
      $('#eix-preview').style.display = '';
      grid.innerHTML = r.entries.map(function (e) {
        var tags = [];
        if (!e.canExtract) tags.push('<span class="f-tag warn">ENCRYPTED</span>');
        return '<div class="file-row">' +
          '<span class="f-icon"><i class="' + getFileIcon(e.FileName) + '"></i></span>' +
          '<span class="f-name" title="' + escapeHtml(e.FileName) + '">' + escapeHtml(e.FileName) + '</span>' +
          tags.join('') +
          '<span class="f-size">' + formatSize(e.RealDataSize) + '</span>' +
          '</div>';
      }).join('');
    } catch (_) {}
  }

  window.pickConvertOutput = function () {
    var path = B().PickSaveFile('VPK Archive (*.vpk)|*.vpk', '.vpk');
    if (path) $('#cv-out').value = path;
  };

  window.convertPak = function () {
    var eix = $('#cv-eix').value.trim();
    var out = $('#cv-out').value.trim();
    if (!eix || !out) return modal('Missing Fields', 'Select both .eix input and .vpk output.');

    var opts = JSON.stringify({
      passphrase: $('#cv-enc').checked ? $('#cv-pass').value : null,
      compressionLevel: parseInt($('#c-comp').value, 10)
    });

    var btn = $('#btn-convert');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Converting...';

    var log = $('#cv-log');
    log.style.display = '';
    $('#cv-log-txt').textContent = 'Starting conversion...\n';

    setTimeout(function () {
      try {
        var r = JSON.parse(B().ConvertEixEpk(eix, out, opts));
        if (r.success) {
          appendLog('Converted: ' + r.convertedFiles + '/' + r.totalEntries + ' files');
          if (r.skippedFiles > 0) appendLog('Skipped: ' + r.skippedFiles + ' (encrypted)');
          if (r.errors && r.errors.length) r.errors.forEach(function (e) { appendLog('ERR: ' + e); });
          appendLog('Done.');
          toast(r.convertedFiles + ' of ' + r.totalEntries + ' files converted.', 'success');
        } else {
          appendLog('FAILED: ' + r.message);
          modal('Error', r.message);
        }
      } catch (e) {
        appendLog('EXCEPTION: ' + e.message);
        modal('Error', e.message || String(e));
      } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-right-left"></i> Convert';
      }
    }, 50);
  };

  function appendLog(msg) {
    var el = $('#cv-log-txt');
    el.textContent += msg + '\n';
    el.scrollTop = el.scrollHeight;
  }

  // ── SETTINGS ──
  function bindSettings() {
    $('#s-comp').addEventListener('input', function () {
      $('#s-comp-val').textContent = $('#s-comp').value;
    });
  }

  function loadPrefs() {
    try {
      var p = JSON.parse(localStorage.getItem('42pak_prefs') || '{}');
      if (p.darkMode === false) {
        document.body.classList.add('light');
        $('#s-dark').checked = false;
      }
      if (p.compression != null) {
        $('#s-comp').value = p.compression;
        $('#s-comp-val').textContent = p.compression;
        $('#c-comp').value = p.compression;
        $('#c-comp-val').textContent = p.compression;
      }
      if (p.encryptByDefault === false) {
        $('#s-enc').checked = false;
        $('#c-enc').checked = false;
        $('#c-enc').dispatchEvent(new Event('change'));
      }
    } catch (_) {}
  }

  function savePrefs() {
    localStorage.setItem('42pak_prefs', JSON.stringify({
      darkMode: !document.body.classList.contains('light'),
      compression: parseInt($('#s-comp').value, 10),
      encryptByDefault: $('#s-enc').checked
    }));
  }

  window.toggleTheme = function () {
    document.body.classList.toggle('light');
    savePrefs();
  };

  // ── KEYBOARD SHORTCUTS ──
  function bindKeyboard() {
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') { window.closeModal(); return; }
      if (!e.ctrlKey) return;
      switch (e.key.toLowerCase()) {
        case 'o': e.preventDefault(); window.openVpkFile(); break;
        case 'n': e.preventDefault(); switchView('create'); break;
        case 'e': e.preventDefault(); if (!$('#btn-extract').disabled) window.extractAllFiles(); break;
        case 'b': e.preventDefault(); window.buildVpk(); break;
      }
    });
  }

  // ── DRAG & DROP ──
  function bindDragDrop() {
    var overlay = $('#drop-overlay');
    var dragCount = 0;

    document.addEventListener('dragenter', function (e) {
      e.preventDefault();
      dragCount++;
      overlay.classList.add('visible');
    });
    document.addEventListener('dragleave', function (e) {
      e.preventDefault();
      dragCount--;
      if (dragCount <= 0) { overlay.classList.remove('visible'); dragCount = 0; }
    });
    document.addEventListener('dragover', function (e) { e.preventDefault(); });
    document.addEventListener('drop', function (e) {
      e.preventDefault();
      dragCount = 0;
      overlay.classList.remove('visible');

      var files = e.dataTransfer.files;
      if (!files || !files.length) return;

      var path = files[0].path || files[0].name;
      if (path.toLowerCase().endsWith('.vpk')) {
        var pass = prompt('Enter passphrase (leave blank if none):') || '';
        openVpkByPath(path, pass);
      } else {
        $('#c-src').value = path;
        switchView('create');
        toast('Source folder set from drop.', 'info');
      }
    });
  }

  // ── MODAL ──
  function modal(title, msg) {
    $('#modal-title').textContent = title;
    $('#modal-body').textContent = msg;
    $('#modal-bg').classList.add('open');
  }

  window.closeModal = function () {
    $('#modal-bg').classList.remove('open');
  };

  // ── TOGGLE PASSWORD ──
  window.toggleVis = function (id) {
    var inp = document.getElementById(id);
    inp.type = inp.type === 'password' ? 'text' : 'password';
  };

  // ── UTILS ──
  function escapeHtml(s) {
    var d = document.createElement('div');
    d.appendChild(document.createTextNode(s));
    return d.innerHTML;
  }

  function escapeAttr(s) {
    return s.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
  }

  function formatSize(bytes) {
    if (bytes == null || bytes === 0) return '0 B';
    var k = 1024;
    var u = ['B', 'KB', 'MB', 'GB'];
    var i = Math.min(Math.floor(Math.log(bytes) / Math.log(k)), u.length - 1);
    return (bytes / Math.pow(k, i)).toFixed(i ? 1 : 0) + ' ' + u[i];
  }

  function getFileIcon(name) {
    var ext = (name.split('.').pop() || '').toLowerCase();
    var map = {
      png: 'fa-solid fa-image', jpg: 'fa-solid fa-image', bmp: 'fa-solid fa-image',
      tga: 'fa-solid fa-image', dds: 'fa-solid fa-image',
      wav: 'fa-solid fa-volume-high', mp3: 'fa-solid fa-music', ogg: 'fa-solid fa-music',
      mse: 'fa-solid fa-clapperboard', msm: 'fa-solid fa-clapperboard',
      gr2: 'fa-solid fa-cube', msa: 'fa-solid fa-person-running',
      py: 'fa-solid fa-code', lua: 'fa-solid fa-code', txt: 'fa-solid fa-file-lines',
      cfg: 'fa-solid fa-sliders', ini: 'fa-solid fa-sliders',
      fnt: 'fa-solid fa-font', ttf: 'fa-solid fa-font',
    };
    return map[ext] || 'fa-solid fa-file';
  }
})();
