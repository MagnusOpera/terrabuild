import {themes as prismThemes} from 'prism-react-renderer';
import {existsSync, readFileSync} from 'node:fs';
import path from 'node:path';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const docsDirs = ['site-docs'];
if (existsSync(path.join(process.cwd(), 'versioned_docs'))) {
  docsDirs.push('versioned_docs');
}
const versionsPath = path.join(process.cwd(), 'versions.json');
const publishedVersions = existsSync(versionsPath)
  ? JSON.parse(readFileSync(versionsPath, 'utf8'))
  : [];
const hasPublishedVersions =
  Array.isArray(publishedVersions) && publishedVersions.length > 0;
const lastVersion = hasPublishedVersions ? publishedVersions[0] : 'current';
const showVersionDropdown = hasPublishedVersions;

const config: Config = {
  title: 'Terrabuild',
  tagline: 'Fast and low ceremony build system for monorepos.',
  favicon: 'favicon.ico',

  url: 'https://terrabuild.io',
  baseUrl: '/',
  organizationName: 'MagnusOpera',
  projectName: 'Terrabuild',
  deploymentBranch: 'gh-pages',

  onBrokenLinks: 'throw',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  markdown: {
    mermaid: true,
  },

  themes: ['@docusaurus/theme-mermaid'],

  presets: [
    [
      'classic',
      {
        docs: {
          path: 'site-docs',
          routeBasePath: 'docs',
          sidebarPath: './sidebars.ts',
          lastVersion,
          versions: {
            current: {
              label: 'Next',
            },
          },
          editUrl: 'https://github.com/MagnusOpera/Terrabuild/tree/main/',
        },
        blog: false,
        pages: false,
        theme: {
          customCss: './theme/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [
    [
      path.resolve(process.cwd(), 'plugins/google-analytics/index.cjs'),
      {
        trackingID: 'G-40K057W4NR',
        allowedHosts: ['terrabuild.io', 'www.terrabuild.io'],
      },
    ],
    [
      '@docusaurus/plugin-content-pages',
      {
        path: 'pages',
        routeBasePath: '/',
        editUrl: 'https://github.com/MagnusOpera/Terrabuild/tree/main/',
      },
    ],
    [
      require.resolve('@easyops-cn/docusaurus-search-local'),
      {
        docsDir: docsDirs,
        indexDocs: true,
        indexBlog: false,
        indexPages: true,
        docsRouteBasePath: '/docs',
        hashed: true,
        explicitSearchResultPath: true,
      },
    ],
  ],

  themeConfig: {
    image: 'images/build-summary.png',
    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'Terrabuild',
      logo: {
        alt: 'Terrabuild Logo',
        src: 'images/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docs',
          label: 'Documentation',
          position: 'left',
        },
        ...(showVersionDropdown
          ? [
              {
                type: 'docsVersionDropdown' as const,
                position: 'left' as const,
                dropdownActiveClassDisabled: true,
              },
            ]
          : []),
        {type: 'search', position: 'right'},
        {
          href: 'https://github.com/MagnusOpera/Terrabuild',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'light',
      links: [],
      copyright:
        '©️2023-present <img src="/images/logo.svg" alt="Magnus Opera" class="tb-footer-mark" /> Magnus Opera SAS',
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['bash', 'fsharp', 'json', 'hcl'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
