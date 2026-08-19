import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://uap.cnbarrier.com',
  trailingSlash: 'never',
  integrations: [
    starlight({
      title: 'Unity Assets Patcher',
      description: 'Inspect Unity assets files and install or uninstall mods.',
      defaultLocale: 'root',
      locales: {
        root: { label: 'English', lang: 'en' },
        'zh-cn': { label: '简体中文', lang: 'zh-CN' },
      },
      social: [
        {
          icon: 'github',
          label: 'GitHub',
          href: 'https://github.com/CnBarrier404/UnityAssetsPatcher',
        },
      ],
      editLink: {
        baseUrl: 'https://github.com/CnBarrier404/UnityAssetsPatcher/edit/main/docs/',
      },
    }),
  ],
});
