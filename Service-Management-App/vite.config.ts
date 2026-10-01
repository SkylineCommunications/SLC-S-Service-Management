import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    base: './',
    build: {
		outDir: '../SLC-Service-Management/PackageContent/CompanionFiles/Skyline DataMiner/Webpages/public/service-management',
    },
})