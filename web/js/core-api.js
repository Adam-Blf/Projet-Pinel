/*
 * Pinel - pont vers le service local et boîtes de dialogue Windows.
 *
 * Le service HTTP tourne sur la boucle locale (127.0.0.1), jamais sur le
 * réseau : rien ne sort du poste. Les boîtes de dialogue (choix d'un
 * dossier ou d'un fichier) passent par la fenêtre hôte WebView2, pas par
 * l'API fichier du navigateur.
 *
 * Expose window.Pinel.api. Dépend de window.Pinel.dom (core-dom.js).
 */
(function () {
  'use strict';

  window.Pinel = window.Pinel || {};
  var dom = window.Pinel.dom;

  var BRIDGE = 'http://127.0.0.1:8787';
  var TOKEN = window.__PINEL_TOKEN || null;

  async function call(path, options) {
    var opts = Object.assign({ headers: {} }, options || {});
    opts.headers['Content-Type'] = 'application/json';
    if (TOKEN) opts.headers.Authorization = 'Bearer ' + TOKEN;
    if (opts.body && typeof opts.body !== 'string') opts.body = JSON.stringify(opts.body);

    var response;
    try {
      response = await fetch(BRIDGE + path, opts);
    } catch (networkError) {
      throw new Error("Service local injoignable. Vérifiez que Pinel est bien lancé.");
    }

    var payload = null;
    try { payload = await response.json(); } catch (e) { payload = null; }

    if (!response.ok) {
      var detail = payload && payload.error
        ? payload.error
        : 'Erreur du service local (code ' + response.status + '). Réessayez ou redémarrez Pinel.';
      throw new Error(detail);
    }
    return payload;
  }

  // ── Boîtes de dialogue Windows, fournies par la fenêtre hôte ───────────
  var pending = new Map();
  var nextId = 1;

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (event) {
      var data = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
      var resolve = pending.get(data && data.id);
      if (!resolve) return;
      pending.delete(data.id);
      resolve(data.payload);
    });
  }

  function askHost(type, extra) {
    if (!(window.chrome && window.chrome.webview)) {
      return Promise.resolve({ error: "Sélecteur indisponible hors de l'application Pinel." });
    }
    var id = 'req' + nextId++;
    return new Promise(function (resolve) {
      pending.set(id, resolve);
      window.chrome.webview.postMessage(JSON.stringify(Object.assign({ type: type, id: id }, extra || {})));
      setTimeout(function () { pending.delete(id); }, 300000);
    });
  }

  function pickFolder() {
    return askHost('selectFolder').then(function (r) {
      if (r && r.error) { dom.toast(r.error); return null; }
      return r && r.folder ? r.folder : null;
    });
  }

  function pickFile(filter) {
    return askHost('selectFile', { filter: filter }).then(function (r) {
      if (r && r.error) { dom.toast(r.error); return null; }
      return r && r.file ? r.file : null;
    });
  }

  // Enveloppe un gestionnaire d'événement asynchrone : toute erreur non
  // rattrapée se termine par une notification lisible plutôt qu'une
  // exception silencieuse dans la console. Le bouton cliqué passe en état
  // occupé (désactivé, aria-busy, icône tournante) jusqu'à la fin de
  // l'appel : un double clic sur "Traiter" ne lance pas deux lots.
  function guard(handler) {
    return function (event) {
      var button = event && event.currentTarget && event.currentTarget.tagName === 'BUTTON'
        ? event.currentTarget : null;
      if (button) {
        if (button.getAttribute('aria-busy') === 'true') return;
        button.setAttribute('aria-busy', 'true');
        button.disabled = true;
      }
      Promise.resolve()
        .then(handler)
        .catch(function (error) { dom.toast('Erreur : ' + error.message, 'error'); })
        .then(function () {
          if (!button) return;
          button.removeAttribute('aria-busy');
          button.disabled = false;
        });
    };
  }

  window.Pinel.api = { call: call, pickFolder: pickFolder, pickFile: pickFile, guard: guard };
})();
