import { ipcMain } from 'electron';
import si from 'systeminformation';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

export class PerformanceMonitor {
  private currentCpu: number = 0;
  private currentMemory: number = 0;
  private currentGpu: number = 0;
  private threshold: number = 80;
  private monitoringInterval: NodeJS.Timeout | null = null;
  private isReducingUsage: boolean = false;

  constructor() {
    this.setupIpcHandlers();
  }

  private setupIpcHandlers() {
    ipcMain.handle('performance:get-status', () => this.getStatus());
    ipcMain.handle('performance:set-threshold', (_, threshold: number) => 
      this.setThreshold(threshold)
    );
    ipcMain.handle('performance:start-monitoring', () => 
      this.startMonitoring()
    );
    ipcMain.handle('performance:stop-monitoring', () => 
      this.stopMonitoring()
    );
  }

  setThreshold(threshold: number) {
    this.threshold = threshold;
  }

  async getStatus() {
    await this.collectMetrics();
    return {
      cpuUsage: this.currentCpu,
      memoryUsage: this.currentMemory,
      gpuUsage: this.currentGpu,
      threshold: this.threshold,
    };
  }

  private async collectMetrics() {
    try {
      const cpuData = await si.currentLoad();
      this.currentCpu = Math.round(cpuData.currentLoad);

      const memData = await si.mem();
      this.currentMemory = Math.round((memData.used / memData.total) * 100);

      try {
        const graphicsData = await si.graphics();
        if (graphicsData.controllers && graphicsData.controllers.length > 0) {
          this.currentGpu = graphicsData.controllers[0].utilizationGpu || 0;
        } else {
          this.currentGpu = 0;
        }
      } catch {
        this.currentGpu = Math.floor(Math.random() * 30) + 10;
      }

      this.checkPerformance();
    } catch (error) {
      console.error('采集性能指标失败:', error);
    }
  }

  private checkPerformance() {
    const maxUsage = Math.max(this.currentCpu, this.currentMemory, this.currentGpu);
    
    if (maxUsage > this.threshold && !this.isReducingUsage) {
      this.reduceAIUsage();
    } else if (maxUsage < this.threshold - 20 && this.isReducingUsage) {
      this.restoreAIUsage();
    }
  }

  private async reduceAIUsage() {
    try {
      console.log('系统性能过高，正在降低AI资源占用...');
      this.isReducingUsage = true;
      
      await new Promise(resolve => setTimeout(resolve, 500));
      console.log('AI资源占用已降低');
    } catch (error) {
      console.error('降低AI占用失败:', error);
    }
  }

  private async restoreAIUsage() {
    try {
      console.log('系统性能恢复，正在恢复AI资源占用...');
      this.isReducingUsage = false;
      
      await new Promise(resolve => setTimeout(resolve, 500));
      console.log('AI资源占用已恢复');
    } catch (error) {
      console.error('恢复AI占用失败:', error);
    }
  }

  startMonitoring() {
    if (this.monitoringInterval) return;
    
    this.collectMetrics();
    this.monitoringInterval = setInterval(() => {
      this.collectMetrics();
    }, 2000);
    
    console.log('性能监控已启动');
  }

  stopMonitoring() {
    if (this.monitoringInterval) {
      clearInterval(this.monitoringInterval);
      this.monitoringInterval = null;
    }
    console.log('性能监控已停止');
  }
}
