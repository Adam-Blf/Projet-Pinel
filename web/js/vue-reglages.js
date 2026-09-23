/*
 * Pinel - écran "Emplacements autorisés" : dossiers de travail, dossier de
 * sortie, dossier des descriptifs de format. Se recharge à chaque affichage
 * de l'écran, via l'événement "pinel:view" diffusé par navigation.js.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  async function loadSettings() {
    var settings = await api.call('/api/reglages');
    var body = dom.$('tbody-dossiers');

    body.innerHTML = (settings.dossiers || []).length
      ? settings.dossiers.map(function (folder) {
          return '<tr><td class="path">' + dom.escapeHtml(folder) + '</td>' +
            '<td class="align-end"><button class="btn ghost small" data-remove="' + dom.escapeHtml(folder) +
            '" type="button">Retirer</button></td></tr>';
        }).join('')
      : '<tr><td colspan="2" class="empty">Aucun dossier ajouté. Ajoutez un dossier pour autoriser Pinel à y ' +
        'lire et à y écrire.</td></tr>';

    dom.els('[data-remove]', body).forEach(function (button) {
      button.addEventListener('click', api.guard(async function () {
        await api.call('/api/reglages/dossier', { method: 'DELETE', body: { chemin: button.dataset.remove } });
        dom.toast('Dossier retiré.');
        loadSettings();
      }));
    });

    renderEtablissement(settings.etablissement || {});
    dom.$('dossier-sortie').textContent = settings.dossierSortie || 'Non défini';
    dom.$('dossier-formats').textContent = settings.dossierFormats || 'Non défini';
  }

  // Le bandeau porte le nom de l'etablissement : sans lui, Pinel affiche son
  // role, jamais le nom d'un autre etablissement.
  var CHAMPS = {
    'etab-nom': 'nom',
    'etab-finess-epmsi': 'finessEPmsi',
    'etab-finess-geo': 'finessGeographique',
    'etab-prestation': 'typeDePrestation',
    'etab-forfait': 'codeForfait',
    'etab-distance': 'classeDistance'
  };

  function renderEtablissement(etablissement) {
    Object.keys(CHAMPS).forEach(function (id) {
      var champ = dom.$(id);
      if (champ) champ.value = etablissement[CHAMPS[id]] || '';
    });
    var bandeau = dom.$('brand-etablissement');
    if (bandeau && etablissement.nom) bandeau.textContent = etablissement.nom;
  }

  function wire() {
    dom.$('btn-etab-enregistrer').addEventListener('click', api.guard(async function () {
      var corps = {};
      Object.keys(CHAMPS).forEach(function (id) {
        corps[CHAMPS[id]] = (dom.$(id).value || '').trim();
      });
      var enregistre = await api.call('/api/reglages/etablissement', { method: 'POST', body: corps });
      renderEtablissement(enregistre);
      dom.toast('Établissement enregistré.');
    }));

    dom.$('btn-dossier-ajouter').addEventListener('click', api.guard(async function () {
      var folder = await api.pickFolder();
      if (!folder) return;
      await api.call('/api/reglages/dossier', { method: 'POST', body: { chemin: folder } });
      await loadSettings();
      dom.toast('Dossier ajouté.');
    }));

    dom.$('btn-dossier-sortie').addEventListener('click', api.guard(async function () {
      var folder = await api.pickFolder();
      if (!folder) return;
      await api.call('/api/reglages/sortie', { method: 'POST', body: { chemin: folder } });
      await loadSettings();
      dom.toast('Dossier de sortie mis à jour.');
    }));

    dom.$('btn-dossier-formats').addEventListener('click', api.guard(async function () {
      var folder = await api.pickFolder();
      if (!folder) return;
      var result = await api.call('/api/reglages/formats', { method: 'POST', body: { chemin: folder } });
      await loadSettings();
      dom.toast(result.descriptifs + ' format(s) décrits.');
    }));

    window.addEventListener('pinel:view', function (event) {
      if (event.detail === 'dossiers') loadSettings().catch(function (e) { dom.toast('Erreur : ' + e.message, 'error'); });
    });
  }

  wire();

  window.Pinel.reglages = { load: loadSettings };
})();
