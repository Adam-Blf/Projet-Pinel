/*
 * Pinel - écran "À propos" : charge le texte de la licence Montserrat
 * (SIL OFL 1.1) depuis le fichier local web/legal/montserrat-ofl.txt.
 *
 * Le fichier est servi par le même hôte virtuel local que le reste de
 * l'application (aucune requête réseau, aucune dépendance à internet). Le
 * chargement est différé au premier dépliage du panneau, pour ne rien
 * demander tant que le médecin n'a pas ouvert la licence.
 *
 * Dépend de window.Pinel.dom.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var loaded = false;

  async function loadLicense() {
    if (loaded) return;
    loaded = true;
    var target = dom.$('licence-montserrat');
    try {
      var response = await fetch('legal/montserrat-ofl.txt');
      if (!response.ok) throw new Error('code ' + response.status);
      target.textContent = await response.text();
    } catch (error) {
      loaded = false;
      target.textContent = "Texte de licence introuvable. Il se trouve normalement dans le dossier " +
        "d'installation de Pinel, sous legal/montserrat-ofl.txt.";
    }
  }

  function wire() {
    var disclosure = dom.$('disclosure-montserrat');
    disclosure.addEventListener('toggle', function () {
      if (disclosure.open) loadLicense().catch(function () { /* message déjà affiché */ });
    });
  }

  wire();
})();
