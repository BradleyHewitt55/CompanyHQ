import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  experimental: {
    serverActions: {
      bodySizeLimit: '6mb',
    },
  },
  output: 'standalone',
  async rewrites() {
    return [
      {
        source: '/logo-frame3.svg',
        destination: '/logo-frame3',
      },
    ];
  },
};

export default nextConfig;
