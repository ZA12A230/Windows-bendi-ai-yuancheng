package system

import (
	"encoding/json"
	"os"
	"os/exec"
	"path/filepath"
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

type DiskInfo struct {
	Path        string  `json:"path"`
	Total       uint64  `json:"total"`
	Used        uint64  `json:"used"`
	Free        uint64  `json:"free"`
	Percent     float64 `json:"percent"`
}

type CleanableFile struct {
	Path     string `json:"path"`
	Size     uint64 `json:"size"`
	Type     string `json:"type"`
	Category string `json:"category"`
}

type InstalledSoftware struct {
	Name            string `json:"name"`
	Version         string `json:"version"`
	Publisher       string `json:"publisher"`
	InstallDate     string `json:"install_date"`
	Size            uint64 `json:"size"`
	UninstallString string `json:"uninstall_string"`
}

type CleanResult struct {
	ScannedFiles int      `json:"scanned_files"`
	DeletedFiles int      `json:"deleted_files"`
	FreedSpace   uint64   `json:"freed_space"`
	Errors       []string `json:"errors"`
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

func GetDiskInfo() ([]DiskInfo, error) {
	partitions, err := disk.Partitions(false)
	if err != nil {
		return nil, err
	}

	var infos []DiskInfo
	for _, p := range partitions {
		if runtime.GOOS == "windows" && len(p.Device) > 0 && p.Device[0] >= 'A' && p.Device[0] <= 'Z' {
			usage, err := disk.Usage(p.Mountpoint)
			if err != nil {
				continue
			}
			infos = append(infos, DiskInfo{
				Path:    p.Mountpoint,
				Total:   usage.Total,
				Used:    usage.Used,
				Free:    usage.Free,
				Percent: usage.UsedPercent,
			})
		} else if runtime.GOOS != "windows" {
			usage, err := disk.Usage(p.Mountpoint)
			if err != nil {
				continue
			}
			infos = append(infos, DiskInfo{
				Path:    p.Mountpoint,
				Total:   usage.Total,
				Used:    usage.Used,
				Free:    usage.Free,
				Percent: usage.UsedPercent,
			})
		}
	}
	return infos, nil
}

var windowsSafePaths = map[string]bool{
	"Windows":         true,
	"Program Files":   true,
	"Program Files (x86)": true,
}

func isWindowsSystemPath(path string) bool {
	normalized := filepath.ToSlash(path)
	for seg := range windowsSafePaths {
		if strings.Contains(normalized, seg) {
			return true
		}
	}
	return false
}

func ScanCleanableFiles() ([]CleanableFile, uint64) {
	var files []CleanableFile
	var totalSize uint64

	if runtime.GOOS == "windows" {
		files, totalSize = scanWindowsCleanable()
	} else {
		files, totalSize = scanLinuxCleanable()
	}

	return files, totalSize
}

func scanWindowsCleanable() ([]CleanableFile, uint64) {
	var files []CleanableFile
	var totalSize uint64

	tempDir := os.Getenv("TEMP")
	if tempDir == "" {
		tempDir = os.Getenv("TMP")
	}
	if tempDir == "" {
		tempDir = `C:\Windows\Temp`
	}

	categories := []struct {
		path     string
		category string
		fileType string
	}{
		{tempDir, "临时文件", "temp"},
		{os.Getenv("LOCALAPPDATA") + `\Temp`, "临时文件", "temp"},
		{os.Getenv("LOCALAPPDATA") + `\Microsoft\Windows\INetCache`, "浏览器缓存", "cache"},
		{os.Getenv("LOCALAPPDATA") + `\Microsoft\Windows\WebCache`, "浏览器缓存", "cache"},
		{os.Getenv("WINDIR") + `\Temp`, "系统临时文件", "temp"},
	}

	for _, cat := range categories {
		if cat.path == "" || cat.path == `\Temp` {
			continue
		}
		scanDir(cat.path, cat.category, cat.fileType, &files, &totalSize)
	}

	scanWindowsPrefetch(&files, &totalSize)
	scanWindowsUpdateCache(&files, &totalSize)
	scanWindowsLogs(&files, &totalSize)

	return files, totalSize
}

func scanDir(dir, category, fileType string, files *[]CleanableFile, totalSize *uint64) {
	entries, err := os.ReadDir(dir)
	if err != nil {
		return
	}

	for _, entry := range entries {
		if entry.IsDir() {
			continue
		}

		info, err := entry.Info()
		if err != nil {
			continue
		}

		fullPath := filepath.Join(dir, entry.Name())
		if isWindowsSystemPath(fullPath) && category != "系统临时文件" {
			continue
		}

		*files = append(*files, CleanableFile{
			Path:     fullPath,
			Size:     uint64(info.Size()),
			Type:     fileType,
			Category: category,
		})
		*totalSize += uint64(info.Size())
	}
}

func scanWindowsPrefetch(files *[]CleanableFile, totalSize *uint64) {
	prefetchDir := os.Getenv("WINDIR") + `\Prefetch`
	scanDir(prefetchDir, "预取文件", "cache", files, totalSize)
}

func scanWindowsUpdateCache(files *[]CleanableFile, totalSize *uint64) {
	updateDir := os.Getenv("WINDIR") + `\SoftwareDistribution\Download`
	scanDir(updateDir, "旧更新文件", "old_update", files, totalSize)
}

func scanWindowsLogs(files *[]CleanableFile, totalSize *uint64) {
	logDir := os.Getenv("WINDIR") + `\Logs`
	scanDir(logDir, "日志文件", "log", files, totalSize)

	cbsLogDir := os.Getenv("WINDIR") + `\Logs\CBS`
	scanDir(cbsLogDir, "日志文件", "log", files, totalSize)
}

func scanLinuxCleanable() ([]CleanableFile, uint64) {
	var files []CleanableFile
	var totalSize uint64

	categories := []struct {
		path     string
		category string
		fileType string
	}{
		{"/tmp", "临时文件", "temp"},
		{"/var/tmp", "临时文件", "temp"},
		{"/var/log", "日志文件", "log"},
		{"/var/cache", "缓存文件", "cache"},
	}

	for _, cat := range categories {
		scanDir(cat.path, cat.category, cat.fileType, &files, &totalSize)
	}

	return files, totalSize
}

func CleanFiles(filePaths []string) (CleanResult, error) {
	result := CleanResult{
		ScannedFiles: len(filePaths),
		Errors:       []string{},
	}

	for _, path := range filePaths {
		if isWindowsSystemPath(path) {
			result.Errors = append(result.Errors, "跳过系统文件: "+path)
			continue
		}

		info, err := os.Stat(path)
		if err != nil {
			result.Errors = append(result.Errors, "无法访问: "+path)
			continue
		}

		if info.IsDir() {
			err = os.RemoveAll(path)
		} else {
			err = os.Remove(path)
		}

		if err != nil {
			result.Errors = append(result.Errors, "删除失败: "+path+" - "+err.Error())
		} else {
			result.DeletedFiles++
			result.FreedSpace += uint64(info.Size())
		}
	}

	return result, nil
}

func ListInstalledSoftware() []InstalledSoftware {
	if runtime.GOOS != "windows" {
		return []InstalledSoftware{}
	}

	return getWindowsInstalledSoftware()
}

func getWindowsInstalledSoftware() []InstalledSoftware {
	var software []InstalledSoftware

	queries := []string{
		`Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* | Select-Object DisplayName, DisplayVersion, Publisher, InstallDate, EstimatedSize, UninstallString | ConvertTo-Json`,
		`Get-ItemProperty HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* | Select-Object DisplayName, DisplayVersion, Publisher, InstallDate, EstimatedSize, UninstallString | ConvertTo-Json`,
	}

	for _, query := range queries {
		cmd := exec.Command("powershell", "-Command", query)
		output, err := cmd.Output()
		if err != nil {
			continue
		}

		var items []map[string]interface{}
		if err := json.Unmarshal(output, &items); err != nil {
			var single map[string]interface{}
			if err := json.Unmarshal(output, &single); err == nil {
				items = []map[string]interface{}{single}
			} else {
				continue
			}
		}

		for _, item := range items {
			name := getString(item, "DisplayName")
			if name == "" || name == " " {
				continue
			}

			size := getUint64(item, "EstimatedSize") * 1024

			software = append(software, InstalledSoftware{
				Name:            name,
				Version:         getString(item, "DisplayVersion"),
				Publisher:       getString(item, "Publisher"),
				InstallDate:     getString(item, "InstallDate"),
				Size:            size,
				UninstallString: getString(item, "UninstallString"),
			})
		}
	}

	return software
}

func getString(m map[string]interface{}, key string) string {
	if v, ok := m[key]; ok {
		if s, ok := v.(string); ok {
			return s
		}
	}
	return ""
}

func getUint64(m map[string]interface{}, key string) uint64 {
	if v, ok := m[key]; ok {
		switch val := v.(type) {
		case float64:
			return uint64(val)
		case int64:
			return uint64(val)
		case uint64:
			return val
		}
	}
	return 0
}

func ForceUninstallSoftware(name string) error {
	if runtime.GOOS != "windows" {
		return nil
	}

	software := ListInstalledSoftware()
	var target *InstalledSoftware
	for _, sw := range software {
		if sw.Name == name {
			target = &sw
			break
		}
	}

	if target == nil {
		return nil
	}

	if target.UninstallString != "" {
		uninstallCmd := target.UninstallString
		if strings.HasPrefix(uninstallCmd, "MsiExec") || strings.HasPrefix(uninstallCmd, "msiexec") {
			cmd := exec.Command("powershell", "-Command",
				`Start-Process msiexec.exe -ArgumentList "/x `+target.Name+` /qn /norestart" -Wait`)
			cmd.Run()
		} else {
			cmd := exec.Command("powershell", "-Command",
				`Start-Process cmd -ArgumentList "/c `+uninstallCmd+` /S" -Wait`)
			cmd.Run()
		}
	}

	programFiles := os.Getenv("ProgramFiles")
	programFilesX86 := os.Getenv("ProgramFiles(x86)")
	appData := os.Getenv("LOCALAPPDATA")

	dirs := []string{
		filepath.Join(programFiles, name),
		filepath.Join(programFilesX86, name),
		filepath.Join(appData, name),
	}

	for _, dir := range dirs {
		os.RemoveAll(dir)
	}

	return nil
}
