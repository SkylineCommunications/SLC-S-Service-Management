import { defineConfig, loadEnv } from 'vite';
import plugin from '@vitejs/plugin-react';
import mockDataMiner from './dev/mockDataMiner.js';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
    const env = loadEnv(mode, process.cwd(), '');
    const useMock = mode === 'mock' || env.VITE_USE_MOCK === 'true';
    const dataMinerOrigin = env.VITE_DATAMINER_ORIGIN?.trim();

    return {
        plugins: [plugin(), ...(useMock ? [mockDataMiner()] : [])],
        base: './',
        server: dataMinerOrigin
            ? {
                proxy: {
                    '/API': { target: dataMinerOrigin, changeOrigin: true, secure: false, ws: true },
                    '/auth': { target: dataMinerOrigin, changeOrigin: true, secure: false },
                },
            }
            : undefined,
        build: {
            outDir: '../SLC-Service-Management/PackageContent/CompanionFiles/Skyline DataMiner/Webpages/public/service-management',
            emptyOutDir: true,
        },
    };
});