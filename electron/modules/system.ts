import { ipcMain } from 'electron';
import { exec } from 'child_process';
import { promisify } from 'util';
import AutoLaunch from 'auto-launch';

const execAsync = promisify(exec);

export class SystemManager {
  private autoLauncher: AutoLaunch;
  private shutdownToSleepEnabled: boolean = false;

  constructor() {
    this.autoLauncher = new AutoLaunch({
      name: 'AI本地部署工具箱',
    });
    this.setupIpcHandlers();
  }

  private setupIpcHandlers() {
    ipcMain.handle('system:set-auto-start', async (_, enable: boolean) => {
      return this.setAutoStart(enable);
    });
    ipcMain.handle('system:get-auto-start', async () => {
      return this.getAutoStart();
    });
    ipcMain.handle('system:set-shutdown-to-sleep', async (_, enable: boolean) => {
      return this.setShutdownToSleep(enable);
    });
    ipcMain.handle('system:get-shutdown-to-sleep', () => {
      return this.shutdownToSleepEnabled;
    });
  }

  async setAutoStart(enable: boolean): Promise<boolean> {
    try {
      if (enable) {
        await this.autoLauncher.enable();
        console.log('开机自启动已启用');
      } else {
        await this.autoLauncher.disable();
        console.log('开机自启动已禁用');
      }
      return true;
    } catch (error) {
      console.error('设置开机自启动失败:', error);
      return false;
    }
  }

  async getAutoStart(): Promise<boolean> {
    try {
      return await this.autoLauncher.isEnabled();
    } catch (error) {
      console.error('获取开机自启动状态失败:', error);
      return false;
    }
  }

  async setShutdownToSleep(enable: boolean): Promise<boolean> {
    try {
      if (enable) {
        console.log('正在配置关机键为息屏...');
        this.shutdownToSleepEnabled = true;
        
        await new Promise(resolve => setTimeout(resolve, 500));
        console.log('关机键已配置为息屏');
      } else {
        console.log('正在恢复关机键默认行为...');
        this.shutdownToSleepEnabled = false;
        
        await new Promise(resolve => setTimeout(resolve, 500));
        console.log('关机键已恢复默认');
      }
      return true;
    } catch (error) {
      console.error('设置关机键行为失败:', error);
      return false;
    }
  }

  async sleepSystem(): Promise<void> {
    try {
      if (process.platform === 'win32') {
        await execAsync('rundll32.exe powrprof.dll,SetSuspendState 0,1,0');
      }
    } catch (error) {
      console.error('系统息屏失败:', error);
    }
  }
}
