package network

import (
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"syscall"
)

type FrpConfig struct {
	Enabled    bool   `json:"enabled"`
	ServerAddr string `json:"server_addr"`
	ServerPort int    `json:"server_port"`
	Token      string `json:"token"`
	LocalPort  int    `json:"local_port"`
	RemotePort int    `json:"remote_port"`
	User       string `json:"user"`
	Password   string `json:"password"`
}

func DownloadFrpClient(isAMD64 bool) error {
	var arch string
	if isAMD64 {
		arch = "amd64"
	} else {
		arch = "arm64"
	}

	if runtime.GOOS == "windows" {
		if runtime.GOARCH == "arm64" {
			arch = "arm64"
		} else {
			arch = "amd64"
		}
	}

	url := fmt.Sprintf("https://github.com/fatedier/frp/releases/latest/download/frp_0.58.1_windows_%s.zip", arch)

	resp, err := http.Get(url)
	if err != nil {
		mirrorURL := fmt.Sprintf("https://ghproxy.net/%s", url)
		resp, err = http.Get(mirrorURL)
		if err != nil {
			return err
		}
	}
	defer resp.Body.Close()

	tmpDir := os.TempDir()
	zipPath := filepath.Join(tmpDir, "frp-client.zip")

	out, err := os.Create(zipPath)
	if err != nil {
		return err
	}
	defer out.Close()

	_, err = io.Copy(out, resp.Body)
	return err
}

func GenerateFrpConfig(config *FrpConfig) error {
	content := fmt.Sprintf(`[common]
server_addr = %s
server_port = %d
token = %s
user = %s

[web]
type = tcp
local_port = %d
remote_port = %d

[ollama]
type = tcp
local_ip = 127.0.0.1
local_port = 11434
remote_port = %d
`, config.ServerAddr, config.ServerPort, config.Token, config.User, config.LocalPort, config.RemotePort, config.RemotePort+1)

	configPath := filepath.Join(os.TempDir(), "frpc.ini")
	return os.WriteFile(configPath, []byte(content), 0644)
}

func StartFrpClient() (*exec.Cmd, error) {
	configPath := filepath.Join(os.TempDir(), "frpc.ini")

	if _, err := os.Stat(configPath); os.IsNotExist(err) {
		return nil, fmt.Errorf("frp config not found")
	}

	exePath := filepath.Join(os.TempDir(), "frpc.exe")
	if _, err := os.Stat(exePath); os.IsNotExist(err) {
		return nil, fmt.Errorf("frp client not found")
	}

	cmd := exec.Command(exePath, "-c", configPath)
	if runtime.GOOS == "windows" {
		cmd.SysProcAttr = &syscall.SysProcAttr{}
	}

	return cmd, cmd.Start()
}

func StopFrpClient(cmd *exec.Cmd) error {
	if cmd != nil && cmd.Process != nil {
		return cmd.Process.Kill()
	}
	return nil
}
