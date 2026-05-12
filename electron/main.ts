import { app, BrowserWindow, Tray, Menu, ipcMain } from 'electron';
import path from 'path';
import { AIManager } from './modules/ai-manager';
import { TunnelManager } from './modules/tunnel';
import { PerformanceMonitor } from './modules/performance';
import { SystemManager } from './modules/system';
import Store from 'electron-store';
import { AI_MODELS, DEFAULT_CONFIG, type SystemConfig } from '../shared/types';

let mainWindow: BrowserWindow | null = null;
let tray: Tray | null = null;

const store = new Store<{ config: SystemConfig }>();

const aiManager = new AIManager();
const tunnelManager = new TunnelManager();
const performanceMonitor = new PerformanceMonitor();
const systemManager = new SystemManager();

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    minWidth: 900,
    minHeight: 600,
    frame: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
    },
  });

  if (process.env.VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(process.env.VITE_DEV_SERVER_URL);
  } else {
    mainWindow.loadFile(path.join(__dirname, '../dist/index.html'));
  }

  mainWindow.on('close', (e) => {
    const config = store.get('config', DEFAULT_CONFIG);
    if (config.runInBackground) {
      e.preventDefault();
      mainWindow?.hide();
    }
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

function createTray() {
  const iconPath = path.join(__dirname, '../assets/icon.png');
  
  tray = new Tray(iconPath);
  
  const contextMenu = Menu.buildFromTemplate([
    {
      label: '显示窗口',
      click: () => {
        mainWindow?.show();
      },
    },
    {
      label: '退出',
      click: () => {
        app.quit();
      },
    },
  ]);
  
  tray.setToolTip('AI本地部署工具箱');
  tray.setContextMenu(contextMenu);
  
  tray.on('double-click', () => {
    mainWindow?.show();
  });
}

app.whenReady().then(async () => {
  aiManager.setModels(AI_MODELS);
  
  const savedConfig = store.get('config', DEFAULT_CONFIG);
  store.set('config', savedConfig);
  
  if (savedConfig.enableTunnel) {
    await tunnelManager.startTunnel();
  }
  
  performanceMonitor.setThreshold(savedConfig.performanceThreshold);
  performanceMonitor.startMonitoring();
  
  if (savedConfig.shutdownToSleep) {
    await systemManager.setShutdownToSleep(true);
  }
  
  createWindow();
  createTray();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

ipcMain.handle('config:get', () => {
  return store.get('config', DEFAULT_CONFIG);
});

ipcMain.handle('config:set', (_, config: SystemConfig) => {
  store.set('config', config);
  return true;
});

process.on('SIGINT', async () => {
  performanceMonitor.stopMonitoring();
  await tunnelManager.stopTunnel();
  app.quit();
});
