/*
 * Pinel - écran "Épisodes de prise en charge" : regroupement des journées
 * consécutives d'un patient en épisodes ambulatoires.
 *
 * Dépend de window.Pinel.dom, window.Pinel.api.
 */
(function () {
  'use strict';

  var dom = window.Pinel.dom;
  var api = window.Pinel.api;

  async function buildEpisodes() {
    var payload = {
      toleranceJours: parseInt(dom.$('episode-tolerance').value, 10) || 0,
      ruptureUm: dom.$('episode-um').checked,
      ruptureSite: dom.$('episode-site').checked,
      fermetureAnnuelle: dom.$('episode-annee').checked
    };
    var result = await api.call('/api/episodes', { method: 'POST', body: payload });

    dom.kpis('kpi-episodes', [
      ['Venues lues', result.venues],
      ['Épisodes', result.episodes],
      ['Durée moyenne', result.dureeMoyenne + ' j'],
      ['Durée > 1 jour', result.superieurs1Jour]
    ]);

    var body = dom.$('tbody-episodes');
    if (!result.apercu || !result.apercu.length) {
      var reason = (result.fichiersIgnores && result.fichiersIgnores.length)
        ? 'Descriptif de format insuffisant pour : ' + result.fichiersIgnores.join(', ') +
          '. Déposez le descriptif officiel dans le dossier des formats.'
        : "Aucune venue trouvée. Traitez d'abord un lot RAA ou RPS.";
      body.innerHTML = '<tr><td colspan="8" class="empty">' + dom.escapeHtml(reason) + '</td></tr>';
      dom.toast('Aucun épisode calculé.');
      return;
    }

    body.innerHTML = result.apercu.map(function (e) {
      return '<tr><td class="mono">' + dom.escapeHtml(e.id) + '</td><td>' + dom.escapeHtml(e.ipp) + '</td>' +
        '<td>' + dom.escapeHtml(e.um) + '</td><td>' + dom.escapeHtml(e.site) + '</td>' +
        '<td>' + dom.escapeHtml(e.debut) + '</td><td>' + dom.escapeHtml(e.fin) + '</td>' +
        '<td class="num">' + dom.escapeHtml(e.venues) + '</td>' +
        '<td class="num">' + dom.escapeHtml(e.duree) + '</td></tr>';
    }).join('');

    dom.toast(result.episodes + ' épisode(s) - règle : ' + result.regle);
  }

  function wire() {
    dom.$('btn-episodes').addEventListener('click', api.guard(buildEpisodes));
  }

  wire();
  dom.kpis('kpi-episodes', [['Venues lues', 0], ['Épisodes', 0], ['Durée moyenne', '0 j'], ['Durée > 1 jour', 0]]);
})();
