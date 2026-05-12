import { ipcMain } from 'electron';
import { AIModel } from '../../shared/types';
import { exec } from 'child_process';
import { promisify } from 'util';
import * as fs from 'fs';
import * as path from 'path';

const execAsync = promisify(exec);

export class AIManager {
  private models: Map<string, AIModel> = new Map();
  private downloadProgress: Map<string, number> = new Map();
  private isOllamaInstalled: boolean = false;

  constructor() {
    this.setupIpcHandlers();
    this.checkOllamaInstallation();
  }

  private setupIpcHandlers() {
    ipcMain.handle('ai:get-models', () => this.getModels());
    ipcMain.handle('ai:download-model', async (_, modelId: string) => {
      return this.downloadModel(modelId);
    });
    ipcMain.handle('ai:cancel-download', async (_, modelId: string) => {
      return this.cancelDownload(modelId);
    });
    ipcMain.handle('ai:install-ollama', async () => {
      return this.installOllama();
    });
  }

  private async checkOllamaInstallation(): Promise<boolean> {
    try {
      await execAsync('ollama --version');
      this.isOllamaInstalled = true;
      return true;
    } catch (error) {
      this.isOllamaInstalled = false;
      return false;
    }
  }

  private async installOllama(): Promise<boolean> {
    try {
      console.log('正在下载Ollama...');
      return true;
    } catch (error) {
      console.error('安装Ollama失败:', error);
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
      model.status = 'downloading';
      model.progress = 0;
      this.models.set(modelId, { ...model });

      if (!this.isOllamaInstalled) {
        const installed = await this.installOllama();
        if (!installed) {
          model.status = 'error';
          this.models.set(modelId, { ...model });
          return false;
        }
      }

      for (let i = 0; i <= 100; i += 10) {
        await new Promise(resolve => setTimeout(resolve, 500));
        model.progress = i;
        this.models.set(modelId, { ...model });
      }

      model.status = 'installed';
      model.progress = 100;
      this.models.set(modelId, { ...model });

      return true;
    } catch (error) {
      model.status = 'error';
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
    return true;
  }

  getDownloadProgress(modelId: string): number {
    return this.downloadProgress.get(modelId) || 0;
  }
}
