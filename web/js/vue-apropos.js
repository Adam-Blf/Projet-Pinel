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
