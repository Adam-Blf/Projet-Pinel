/*
 * Pinel - écran "À propos" : charge le texte des licences tierces
 * (Montserrat, SIL OFL 1.1 ; Phosphor Icons, MIT) depuis web/legal/.
 *
 * Chaque panneau repliable porte data-licence, le chemin local du texte.
 * Le fichier est servi par le même hôte virtuel local que le reste de
 * l'application (aucune requête réseau, aucune dépendance à internet). Le
 * chargement est différé au premier dépliage, pour ne rien demander tant
 * que personne n'a ouvert la licence.
 *
 * Dépend de window.Pinel.dom.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;

  async function loadLicense(disclosure) {
    if (disclosure.dataset.loaded) return;
    disclosure.dataset.loaded = 'true';
    var path = disclosure.dataset.licence;
    var target = dom.el('.license-text', disclosure);
    try {
      var response = await fetch(path);
      if (!response.ok) throw new Error('code ' + response.status);
      target.textContent = await response.text();
    } catch (error) {
      delete disclosure.dataset.loaded;
      target.textContent = "Texte de licence introuvable. Il se trouve normalement dans le dossier " +
        "d'installation de Pinel, sous " + path + '.';
    }
  }

  function wire() {
    dom.els('[data-licence]').forEach(function (disclosure) {
      disclosure.addEventListener('toggle', function () {
        if (disclosure.open) loadLicense(disclosure).catch(function () { /* message déjà affiché */ });
      });
    });
  }

  wire();
})();

/*
 * Mise à jour depuis le dossier du GHT. Le bouton d'installation n'apparaît
 * que lorsqu'une version plus récente attend : proposer une action qui ne fera
 * rien est pire que ne rien proposer.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  function render(state) {
    dom.$('maj-etat').textContent = 'Version ' + state.version +
      (state.available ? ', version ' + state.available + ' disponible. ' : '. ') + (state.note || '');
    dom.$('maj-dossier').textContent = state.source || 'Aucun dossier de mise à jour configuré.';
    dom.$('btn-maj-installer').hidden = !state.available;
  }

  async function check() {
    render(await api.call('/api/maj'));
  }

  function wire() {
    dom.$('btn-maj-verifier').addEventListener('click', api.guard(check));

    dom.$('btn-maj-dossier').addEventListener('click', api.guard(async function () {
      var folder = await api.pickFolder();
      if (!folder) return;
      await api.call('/api/maj/dossier', { method: 'POST', body: { chemin: folder } });
      await check();
      dom.toast('Dossier de mise à jour enregistré.');
    }));

    dom.$('btn-maj-installer').addEventListener('click', api.guard(async function () {
      dom.toast('Téléchargement de la mise à jour…');
      var state = await api.call('/api/maj/installer', { method: 'POST' });
      render(state);
      dom.toast(state.note || 'Mise à jour installée.');
    }));

    window.addEventListener('pinel:view', function (event) {
      if (event.detail === 'apropos') check().catch(function () { /* service pas encore prêt */ });
    });
  }

  wire();
})();

/*
 * Licence de l'établissement. L'état est affiché tel quel, y compris pendant
 * la tolérance qui suit l'échéance : un DIM doit savoir qu'il est en sursis,
 * pas le découvrir le jour où l'outil se ferme.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  function render(state) {
    dom.$('licence-etat').textContent = state.reason;
    dom.$('licence-detail').textContent = state.content
      ? state.content.etablissement + ' - FINESS ' + state.content.finess + ' - licence ' + state.content.numero
      : '';
  }

  async function check() {
    render(await api.call('/api/licence'));
  }

  function wire() {
    dom.$('btn-licence-importer').addEventListener('click', api.guard(async function () {
      var file = await api.pickFile('Licence Pinel|*.lic|Tous les fichiers|*.*');
      if (!file) return;
      var state = await api.call('/api/licence/importer', { method: 'POST', body: { fichier: file } });
      render(state);
      dom.toast(state.valid ? 'Licence installée.' : state.reason, state.valid ? undefined : 'error');
    }));

    window.addEventListener('pinel:view', function (event) {
      if (event.detail === 'apropos') check().catch(function () { /* service pas encore prêt */ });
    });
  }

  wire();
})();
