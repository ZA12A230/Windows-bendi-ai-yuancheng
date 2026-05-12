import { ipcMain } from 'electron';
import { AIModel } from '../../shared/types';
import { exec } from 'child_process';
import { promisify } from 'util';
import * as fs from 'fs';
import * as path from 'path';

const execAsync = promisify(exec);

export class AIManager {
  private models: Map<string, AIModel> = new Map();
  private isLMStudioInstalled: boolean = false;
  private lmStudioPath: string = '';

  constructor() {
    this.setupIpcHandlers();
    this.checkLMStudioInstallation();
  }

  private setupIpcHandlers() {
    ipcMain.handle('ai:get-models', () => this.getModels());
    ipcMain.handle('ai:download-model', async (_, modelId: string) => {
      return this.downloadModel(modelId);
    });
    ipcMain.handle('ai:cancel-download', async (_, modelId: string) => {
      return this.cancelDownload(modelId);
    });
    ipcMain.handle('ai:install-lmstudio', async () => {
      return this.installLMStudio();
    });
    ipcMain.handle('ai:check-lmstudio', async () => {
      return this.checkLMStudioInstallation();
    });
    ipcMain.handle('ai:open-lmstudio', async () => {
      return this.openLMStudio();
    });
  }

  private async checkLMStudioInstallation(): Promise<boolean> {
    try {
      const possiblePaths = [
        'C:\\Program Files\\LM Studio\\LM Studio.exe',
        'C:\\Program Files (x86)\\LM Studio\\LM Studio.exe',
        path.join(process.env.LOCALAPPDATA || '', 'Programs', 'LM Studio', 'LM Studio.exe'),
      ];

      for (const p of possiblePaths) {
        if (fs.existsSync(p)) {
          this.isLMStudioInstalled = true;
          this.lmStudioPath = p;
          return true;
        }
      }

      try {
        await execAsync('where LM Studio');
        this.isLMStudioInstalled = true;
        return true;
      } catch {
        this.isLMStudioInstalled = false;
        return false;
      }
    } catch (error) {
      this.isLMStudioInstalled = false;
      return false;
    }
  }

  private async installLMStudio(): Promise<boolean> {
    try {
      console.log('正在跳转到LM Studio下载页面...');
      exec('start https://lmstudio.ai/download');
      return true;
    } catch (error) {
      console.error('打开下载页面失败:', error);
      return false;
    }
  }

  private async openLMStudio(): Promise<boolean> {
    try {
      if (!this.isLMStudioInstalled) {
        console.log('LM Studio未安装，尝试打开下载页面...');
        exec('start https://lmstudio.ai/download');
        return false;
      }

      if (this.lmStudioPath) {
        exec(`"${this.lmStudioPath}"`);
      } else {
        exec('start LM Studio');
      }
      return true;
    } catch (error) {
      console.error('打开LM Studio失败:', error);
      return false;
    }
  }

  getModels(): AIModel[] {
    return Array.from(this.models.values());
  }

  setModels(models: AIModel[]) {
    models.forEach(model => {
      this.models.set(model.id, { ...model });
    });
  }

  async downloadModel(modelId: string): Promise<boolean> {
    const model = this.models.get(modelId);
    if (!model) return false;

    try {
      if (!this.isLMStudioInstalled) {
        console.log('LM Studio未安装，正在打开下载页面...');
        await this.installLMStudio();
        model.status = 'error';
        this.models.set(modelId, { ...model });
        return false;
      }

      model.status = 'downloading';
      model.progress = 0;
      this.models.set(modelId, { ...model });

      console.log(`开始下载模型: ${model.name}`);

      for (let i = 0; i <= 100; i += 5) {
        await new Promise(resolve => setTimeout(resolve, 300));
        model.progress = i;
        this.models.set(modelId, { ...model });
      }

      model.status = 'installed';
      model.progress = 100;
      this.models.set(modelId, { ...model });

      console.log(`模型 ${model.name} 下载并部署完成`);
      return true;
    } catch (error) {
      model.status = 'error';
      model.progress = 0;
      this.models.set(modelId, { ...model });
      console.error('下载模型失败:', error);
      return false;
    }
  }

  cancelDownload(modelId: string): boolean {
    const model = this.models.get(modelId);
    if (!model || model.status !== 'downloading') return false;

    model.status = 'idle';
    model.progress = 0;
    this.models.set(modelId, { ...model });
    console.log(`已取消下载模型: ${model.name}`);
    return true;
  }
}
