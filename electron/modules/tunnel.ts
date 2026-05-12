import { ipcMain } from 'electron';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

export class TunnelManager {
  private isConnected: boolean = false;
  private tunnelUrl: string | null = null;
  private tunnelProcess: any = null;

  constructor() {
    this.setupIpcHandlers();
  }

  private setupIpcHandlers() {
    ipcMain.handle('tunnel:start', async () => this.startTunnel());
    ipcMain.handle('tunnel:stop', async () => this.stopTunnel());
    ipcMain.handle('tunnel:status', () => this.getStatus());
  }

  async startTunnel(): Promise<boolean> {
    try {
      console.log('正在启动内网穿透服务...');
      
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      this.tunnelUrl = 'https://demo-tunnel.example.com:12345';
      this.isConnected = true;
      
      console.log('内网穿透已启动:', this.tunnelUrl);
      return true;
    } catch (error) {
      console.error('启动内网穿透失败:', error);
      return false;
    }
  }

  async stopTunnel(): Promise<boolean> {
    try {
      if (this.tunnelProcess) {
        this.tunnelProcess.kill();
        this.tunnelProcess = null;
      }
      
      this.isConnected = false;
      this.tunnelUrl = null;
      console.log('内网穿透已停止');
      return true;
    } catch (error) {
      console.error('停止内网穿透失败:', error);
      return false;
    }
  }

  getStatus() {
    return {
      connected: this.isConnected,
      url: this.tunnelUrl,
    };
  }
}
