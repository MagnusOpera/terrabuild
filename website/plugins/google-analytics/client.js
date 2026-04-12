export default {
  onRouteDidUpdate({location, previousLocation}) {
    const analytics = window.__tbAnalytics;

    if (
      !analytics?.enabled ||
      typeof window.gtag !== 'function' ||
      !previousLocation
    ) {
      return;
    }

    if (
      location.pathname === previousLocation.pathname &&
      location.search === previousLocation.search &&
      location.hash === previousLocation.hash
    ) {
      return;
    }

    setTimeout(() => {
      if (typeof window.gtag !== 'function') {
        return;
      }

      window.gtag(
        'set',
        'page_path',
        location.pathname + location.search + location.hash,
      );
      window.gtag('event', 'page_view');
    });
  },
};
