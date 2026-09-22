/*
 * Pinel - thème clair ou sombre.
 *
 * Chargé dans <head>, sans différé : data-theme doit être posé avant le
 * premier rendu, sinon l'écran s'affiche un instant dans le mauvais thème.
 *
 * Tant que l'utilisateur n'a rien choisi, Pinel suit le réglage de Windows
 * et le suit encore s'il change en cours de session. Un clic sur la bascule
 * enregistre le choix sur le poste (localStorage du profil WebView2), qui
 * prime ensuite sur le système.
 *
 * Expose window.Pinel.theme. Aucune dépendance.
 */
(function () {
  'use strict';

  window.Pinel = window.Pinel || {};

  var KEY = 'pinel.theme';
  var media = window.matchMedia('(prefers-color-scheme: dark)');

  function stored() {
    try { return localStorage.getItem(KEY); } catch (e) { return null; }
  }

  function current() {
    return document.documentElement.getAttribute('data-theme') || 'light';
  }

  function label(button) {
    var dark = current() === 'dark';
    button.setAttribute('aria-label', dark ? 'Passer au thème clair' : 'Passer au thème sombre');
    button.title = button.getAttribute('aria-label');
    var icon = button.querySelector('[data-icon]');
    if (icon) {
      var url = 'url("icons/' + (dark ? 'sun' : 'moon') + '.svg")';
      icon.style.webkitMaskImage = url;
      icon.style.maskImage = url;
    }
  }

  function apply(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    var button = document.getElementById('btn-theme');
    if (button) label(button);
  }

  function toggle() {
    var next = current() === 'dark' ? 'light' : 'dark';
    try { localStorage.setItem(KEY, next); } catch (e) { /* choix non mémorisé, bascule quand même */ }
    apply(next);
  }

  function wire() {
    var button = document.getElementById('btn-theme');
    if (!button) return;
    button.addEventListener('click', toggle);
    label(button);
  }

  apply(stored() || (media.matches ? 'dark' : 'light'));
  media.addEventListener('change', function (event) {
    if (!stored()) apply(event.matches ? 'dark' : 'light');
  });

  window.Pinel.theme = { wire: wire };
})();
