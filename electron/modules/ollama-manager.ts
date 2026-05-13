import { ipcMain } from 'electron';
import { exec, spawn } from 'child_process';
import { promisify } from 'util';
import * as fs from 'fs';
import * as path from 'path';
import * as https from 'https';

const execAsync = promisify(exec);

export class OllamaManager {
  private isInstalled: boolean = false;
  private installPath: string = '';
  private downloading: boolean = false;

  constructor() {
    this.setupIpcHandlers();
  }

  private setupIpcHandlers() {
    ipcMain.handle('ollama:check', async () => this.checkInstallation());
    ipcMain.handle('ollama:install', async () => this.installOllama());
    ipcMain.handle('ollama:list_models', async () => this.listLocalModels());
    ipcMain.handle('ollama:pull_model', async (_, modelId: string) => this.pullModel(modelId));
    ipcMain.handle('ollama:is_running', async () => this.isOllamaRunning());
  }

  async checkInstallation(): Promise<{ installed: boolean; path: string }> {
    try {
      const possiblePaths = [
        'C:\\Program Files\\Ollama\\ollama.exe',
        path.join(process.env.USERPROFILE || '', 'AppData\\Local\\Programs\\Ollama\\ollama.exe'),
      ];

      for (const p of possiblePaths) {
        if (fs.existsSync(p)) {
          this.isInstalled = true;
          this.installPath = p;
          return { installed: true, path: p };
        }
      }

      try {
        await execAsync('ollama --version');
        this.isInstalled = true;
        return { installed: true, path: 'ollama' };
      } catch {
      }

      this.isInstalled = false;
      return { installed: false, path: '' };
    } catch (error) {
      this.isInstalled = false;
      return { installed: false, path: '' };
    }
  }

  async installOllama(): Promise<boolean> {
    try {
      if (this.downloading) return false;
      this.downloading = true;

      const installerPath = path.join(process.env.TEMP || '', 'ollama-installer.exe');
      
      if (fs.existsSync(installerPath)) {
        fs.unlinkSync(installerPath);
      }

      await this.downloadFile('https://ollama.com/download/windows', installerPath);

      if (fs.existsSync(installerPath)) {
        const installProc = spawn(installerPath, ['/S']);
        
        await new Promise(resolve => {
          installProc.on('exit', resolve);
          setTimeout(resolve, 60000); 
        });

        try {
          fs.unlinkSync(installerPath);
        } catch {
        }

        await new Promise(resolve => setTimeout(resolve, 3000));

        const check = await this.checkInstallation();
        this.downloading = false;
        return check.installed;
      }

      this.downloading = false;
      return false;
    } catch (error) {
      this.downloading = false;
      return false;
    }
  }

  private downloadFile(url: string, destination: string): Promise<void> {
    return new Promise((resolve, reject) => {
      https.get(url, (response) => {
        if (response.statusCode === 302 && response.headers.location) {
          return this.downloadFile(response.headers.location, destination).then(resolve).catch(reject);
        }

        const file = fs.createWriteStream(destination);
        response.pipe(file);

        file.on('finish', () => {
          file.close();
          resolve();
        });

        file.on('error', (error) => {
          fs.unlink(destination, () => reject(error));
        });
      }).on('error', reject);
    });
  }

  async listLocalModels(): Promise<string[]> {
    try {
      const { stdout } = await execAsync('ollama list');
      const lines = stdout.trim().split('\n');
      if (lines.length <= 1) return [];

      return lines.slice(1)
        .map(line => line.trim().split(/\s+/)[0])
        .filter(name => name.length > 0);
    } catch (error) {
      return [];
    }
  }

  async pullModel(modelName: string): Promise<{ success: boolean; progress: number }> {
    return new Promise((resolve) => {
      const process = spawn('ollama', ['pull', modelName]);
      let lastProgress = 0;

      process.stdout.on('data', (data) => {
        const str = data.toString();
        const match = str.match(/(\d+)%/);
        if (match) {
          lastProgress = parseInt(match[1]);
        }
      });

      process.on('exit', (code) => {
        resolve({ success: code === 0, progress: code === 0 ? 100 : lastProgress });
      });

      setTimeout(() => {
        resolve({ success: true, progress: 100 });
      }, 300000);
    });
  }

  async isOllamaRunning(): Promise<boolean> {
    try {
      await execAsync('netstat -ano | findstr :11434');
      return true;
    } catch {
      try {
        spawn('ollama', ['serve'], { detached: true });
        await new Promise(resolve => setTimeout(resolve, 3000));
        return true;
      } catch {
        return false;
      }
    }
  }
}
