package system

import (
	"os"
	"os/exec"
	"runtime"
	"strings"

	"github.com/shirou/gopsutil/v4/cpu"
	"github.com/shirou/gopsutil/v4/disk"
	"github.com/shirou/gopsutil/v4/host"
	"github.com/shirou/gopsutil/v4/mem"
	"github.com/shirou/gopsutil/v4/net"
)

type SystemStats struct {
	CPUUsage       float64  `json:"cpu_usage"`
	MemoryTotal    uint64   `json:"memory_total"`
	MemoryUsed     uint64   `json:"memory_used"`
	MemoryPercent  float64  `json:"memory_percent"`
	DiskTotal      uint64   `json:"disk_total"`
	DiskUsed       uint64   `json:"disk_used"`
	DiskPercent    float64  `json:"disk_percent"`
	NetSent        uint64   `json:"net_sent"`
	NetRecv        uint64   `json:"net_recv"`
	Uptime         uint64   `json:"uptime"`
	GPUUsage       float64  `json:"gpu_usage"`
	GPUMemoryTotal uint64   `json:"gpu_memory_total"`
	GPUMemoryUsed  uint64   `json:"gpu_memory_used"`
	ProcessStats   []ProcessInfo `json:"process_stats"`
}

type ProcessInfo struct {
	Name      string  `json:"name"`
	CPU       float64 `json:"cpu"`
	Memory    float64 `json:"memory"`
	PID       int32   `json:"pid"`
}

func GetSystemStats() (*SystemStats, error) {
	stats := &SystemStats{}

	cpuPercents, err := cpu.Percent(0, false)
	if err == nil && len(cpuPercents) > 0 {
		stats.CPUUsage = cpuPercents[0]
	}

	memInfo, err := mem.VirtualMemory()
	if err == nil {
		stats.MemoryTotal = memInfo.Total
		stats.MemoryUsed = memInfo.Used
		stats.MemoryPercent = memInfo.UsedPercent
	}

	diskInfo, err := disk.Usage("C:")
	if err != nil {
		diskInfo, err = disk.Usage("/")
	}
	if err == nil {
		stats.DiskTotal = diskInfo.Total
		stats.DiskUsed = diskInfo.Used
		stats.DiskPercent = diskInfo.UsedPercent
	}

	netStats, err := net.IOCounters(false)
	if err == nil && len(netStats) > 0 {
		stats.NetSent = netStats[0].BytesSent
		stats.NetRecv = netStats[0].BytesRecv
	}

	hostInfo, err := host.Info()
	if err == nil {
		stats.Uptime = hostInfo.Uptime
	}

	if runtime.GOOS == "windows" {
		gpuUsage, gpuMemTotal, gpuMemUsed := getWindowsGPUStats()
		stats.GPUUsage = gpuUsage
		stats.GPUMemoryTotal = gpuMemTotal
		stats.GPUMemoryUsed = gpuMemUsed
	}

	return stats, nil
}

func getWindowsGPUStats() (usage float64, memTotal uint64, memUsed uint64) {
	if runtime.GOOS != "windows" {
		return 0, 0, 0
	}

	cmd := exec.Command("powershell", "-Command",
		`Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty AdapterRAM`)
	output, err := cmd.Output()
	if err != nil {
		return 0, 0, 0
	}

	memStr := strings.TrimSpace(string(output))
	var total uint64
	if _, err := strings.NewReader(memStr).Read([]byte{}); err == nil {
		// Simple parsing, in production use proper parsing
	}

	return 0, total, 0
}

func SetAutoStartup(enable bool) error {
	if runtime.GOOS != "windows" {
		return nil
	}

	exePath, err := exec.LookPath(os.Args[0])
	if err != nil {
		return err
	}

	if enable {
		cmd := exec.Command("powershell", "-Command",
			`$WshShell = New-Object -ComObject WScript.Shell; `+
				`$Shortcut = $WshShell.CreateShortcut("$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\LocalAI.lnk"); `+
				`$Shortcut.TargetPath = "`+exePath+`"; `+
				`$Shortcut.Save()`)
		return cmd.Run()
	} else {
		cmd := exec.Command("powershell", "-Command",
			`Remove-Item "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\LocalAI.lnk" -ErrorAction SilentlyContinue`)
		return cmd.Run()
	}
}

func SetSleepOnShutdown(enable bool) error {
	if runtime.GOOS != "windows" {
		return nil
	}

	if enable {
		cmd := exec.Command("powershell", "-Command",
			`$action = New-ScheduledTaskAction -Execute "powershell" -Argument "-Command (Add-Type '[DllImport(\"user32.dll\")]public static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);' -Name a -Passthru)::SendMessage(-1, 0x0112, 0xF170, 2)"; `+
				`$trigger = New-ScheduledTaskTrigger -AtLogOn; `+
				`Register-ScheduledTask -TaskName "SleepOnShutdown" -Action $action -Trigger $trigger -Force`)
		return cmd.Run()
	} else {
		cmd := exec.Command("powershell", "-Command",
			`Unregister-ScheduledTask -TaskName "SleepOnShutdown" -Confirm:$false -ErrorAction SilentlyContinue`)
		return cmd.Run()
	}
}

func SetScreenLock() error {
	if runtime.GOOS != "windows" {
		return nil
	}

	cmd := exec.Command("rundll32.exe", "user32.dll,LockWorkStation")
	return cmd.Run()
}

func GoToSleep() error {
	if runtime.GOOS != "windows" {
		return nil
	}

	cmd := exec.Command("powershell", "-Command",
		`(Add-Type '[DllImport(\"powrprof.dll\")]public static extern bool SetSuspendState(bool hiber, bool forceCritical, bool disableWakeEvent);' -Name a -Passthru)::SetSuspendState($false, $true, $false)`)
	return cmd.Run()
}

type PowerAction int

const (
	PowerShutdown PowerAction = iota
	PowerSleep
	PowerHibernate
)

func ExecutePowerAction(action PowerAction) error {
	if runtime.GOOS != "windows" {
		return nil
	}

	switch action {
	case PowerSleep:
		return GoToSleep()
	case PowerHibernate:
		cmd := exec.Command("powershell", "-Command",
			`(Add-Type '[DllImport(\"powrprof.dll\")]public static extern bool SetSuspendState(bool hiber, bool forceCritical, bool disableWakeEvent);' -Name a -Passthru)::SetSuspendState($true, $true, $false)`)
		return cmd.Run()
	case PowerShutdown:
		cmd := exec.Command("shutdown", "/s", "/t", "0")
		return cmd.Run()
	}

	return nil
}

func GetIPv4Address() (string, error) {
	cmd := exec.Command("powershell", "-Command",
		`(Get-NetIPAddress -AddressFamily IPv4 -InterfaceAlias Ethernet*,Wi-Fi* | Where-Object { $_.IPAddress -notlike "169.254.*" } | Select-Object -First 1 -ExpandProperty IPAddress)`)
	output, err := cmd.Output()
	if err != nil {
		return "", err
	}
	return strings.TrimSpace(string(output)), nil
}
