const path = require('node:path');

module.exports = function pluginSafeGoogleAnalytics(_context, options) {
  const trackingID = options.trackingID;
  const allowedHosts = options.allowedHosts ?? ['terrabuild.io'];
  const allowedHostsJson = JSON.stringify(allowedHosts);
  const trackingIDJson = JSON.stringify(trackingID);

  return {
    name: 'terrabuild-safe-google-analytics',
    getClientModules() {
      return [path.join(__dirname, 'client.js')];
    },
    injectHtmlTags() {
      return {
        headTags: [
          {
            tagName: 'link',
            attributes: {
              rel: 'preconnect',
              href: 'https://www.google-analytics.com',
            },
          },
          {
            tagName: 'link',
            attributes: {
              rel: 'preconnect',
              href: 'https://www.googletagmanager.com',
            },
          },
          {
            tagName: 'script',
            innerHTML: `
              window.__tbAnalytics = {
                trackingID: ${trackingIDJson},
                allowedHosts: ${allowedHostsJson},
                enabled: ${allowedHostsJson}.includes(window.location.hostname)
              };
              window.dataLayer = window.dataLayer || [];
              window.gtag = window.gtag || function(){window.dataLayer.push(arguments);};
              if (window.__tbAnalytics.enabled) {
                var gtagScript = document.createElement('script');
                gtagScript.async = true;
                gtagScript.src = 'https://www.googletagmanager.com/gtag/js?id=' + ${trackingIDJson};
                document.head.appendChild(gtagScript);
                window.gtag('js', new Date());
                window.gtag('config', ${trackingIDJson}, {});
              }
            `,
          },
        ],
      };
    },
  };
};
