package remote

import (
	"encoding/base64"
	"encoding/json"
	"fmt"
	"image"
	"image/jpeg"
	"net/http"
	"os/exec"
	"runtime"
	"strings"
	"sync"

	"golang.org/x/net/websocket"
)

type RemoteControl struct {
	Port      int
	Server    *http.Server
	mu        sync.Mutex
	clients   map[string]*websocket.Conn
}

type InputEvent struct {
	Type       string  `json:"type"`
	Key        string  `json:"key,omitempty"`
	X          int     `json:"x,omitempty"`
	Y          int     `json:"y,omitempty"`
	Button     string  `json:"button,omitempty"`
	DeltaX     int     `json:"deltaX,omitempty"`
	DeltaY     int     `json:"deltaY,omitempty"`
}

func NewRemoteControl(port int) *RemoteControl {
	return &RemoteControl{
		Port:    port,
		clients: make(map[string]*websocket.Conn),
	}
}

func (rc *RemoteControl) Start() error {
	mux := http.NewServeMux()

	mux.HandleFunc("/", rc.handleWeb)
	mux.Handle("/ws", websocket.Handler(rc.handleWebSocket))
	mux.HandleFunc("/screen", rc.handleScreen)
	mux.HandleFunc("/input", rc.handleInput)

	rc.Server = &http.Server{
		Addr:    fmt.Sprintf(":%d", rc.Port),
		Handler: mux,
	}

	return rc.Server.ListenAndServe()
}

func (rc *RemoteControl) Stop() error {
	if rc.Server != nil {
		return rc.Server.Close()
	}
	return nil
}

func (rc *RemoteControl) handleWeb(w http.ResponseWriter, r *http.Request) {
	html := `<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>远程桌面控制</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { background: #1a1a2e; color: #eee; font-family: 'Segoe UI', sans-serif; }
        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
        h1 { margin-bottom: 20px; }
        #screen { border: 2px solid #4a4a6a; border-radius: 8px; max-width: 100%; cursor: crosshair; }
        .status { margin: 10px 0; padding: 10px; background: #2a2a4a; border-radius: 4px; }
        .controls { margin: 20px 0; }
        button { padding: 10px 20px; margin: 5px; border: none; border-radius: 4px; 
                 background: #4a4a8a; color: white; cursor: pointer; }
        button:hover { background: #5a5a9a; }
    </style>
</head>
<body>
    <div class="container">
        <h1>远程桌面控制</h1>
        <div class="status" id="status">连接中...</div>
        <div class="controls">
            <button onclick="sendKey('ctrl+alt+del')">Ctrl+Alt+Del</button>
            <button onclick="sendKey('win')">Windows 键</button>
            <button onclick="sendKey('fullscreen')">全屏</button>
        </div>
        <canvas id="screen"></canvas>
    </div>
    <script>
        let ws;
        const canvas = document.getElementById('screen');
        const ctx = canvas.getContext('2d');
        const status = document.getElementById('status');
        
        function connect() {
            ws = new WebSocket('ws://' + window.location.host + '/ws');
            ws.onopen = () => { status.textContent = '已连接'; status.style.background = '#2a5a2a'; };
            ws.onclose = () => { status.textContent = '已断开，重连中...'; status.style.background = '#8a2a2a'; setTimeout(connect, 3000); };
            ws.onmessage = (e) => {
                const img = new Image();
                img.onload = () => {
                    canvas.width = img.width;
                    canvas.height = img.height;
                    ctx.drawImage(img, 0, 0);
                };
                img.src = 'data:image/jpeg;base64,' + e.data;
            };
        }
        
        canvas.addEventListener('mousemove', e => {
            if (ws.readyState === WebSocket.OPEN) {
                const rect = canvas.getBoundingClientRect();
                const scaleX = canvas.width / rect.width;
                const scaleY = canvas.height / rect.height;
                ws.send(JSON.stringify({
                    type: 'mousemove',
                    x: Math.floor((e.clientX - rect.left) * scaleX),
                    y: Math.floor((e.clientY - rect.top) * scaleY)
                }));
            }
        });
        
        canvas.addEventListener('click', e => {
            if (ws.readyState === WebSocket.OPEN) {
                const rect = canvas.getBoundingClientRect();
                const scaleX = canvas.width / rect.width;
                const scaleY = canvas.height / rect.height;
                ws.send(JSON.stringify({
                    type: 'click',
                    x: Math.floor((e.clientX - rect.left) * scaleX),
                    y: Math.floor((e.clientY - rect.top) * scaleY),
                    button: 'left'
                }));
            }
        });
        
        canvas.addEventListener('contextmenu', e => {
            e.preventDefault();
            if (ws.readyState === WebSocket.OPEN) {
                const rect = canvas.getBoundingClientRect();
                const scaleX = canvas.width / rect.width;
                const scaleY = canvas.height / rect.height;
                ws.send(JSON.stringify({
                    type: 'click',
                    x: Math.floor((e.clientX - rect.left) * scaleX),
                    y: Math.floor((e.clientY - rect.top) * scaleY),
                    button: 'right'
                }));
            }
        });
        
        canvas.addEventListener('wheel', e => {
            if (ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({
                    type: 'wheel',
                    deltaX: Math.floor(e.deltaX),
                    deltaY: Math.floor(e.deltaY)
                }));
            }
        });
        
        document.addEventListener('keydown', e => {
            if (ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'keydown', key: e.key }));
            }
        });
        
        document.addEventListener('keyup', e => {
            if (ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'keyup', key: e.key }));
            }
        });
        
        function sendKey(combo) {
            if (ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'keycombo', key: combo }));
            }
        }
        
        connect();
    </script>
</body>
</html>`
	w.Header().Set("Content-Type", "text/html")
	w.Write([]byte(html))
}

func (rc *RemoteControl) handleWebSocket(ws *websocket.Conn) {
	rc.mu.Lock()
	clientID := fmt.Sprintf("%p", ws)
	rc.clients[clientID] = ws
	rc.mu.Unlock()

	defer func() {
		rc.mu.Lock()
		delete(rc.clients, clientID)
		rc.mu.Unlock()
		ws.Close()
	}()

	for {
		var msg string
		if err := websocket.Message.Receive(ws, &msg); err != nil {
			break
		}

		var event InputEvent
		if err := json.Unmarshal([]byte(msg), &event); err != nil {
			continue
		}

		rc.processInput(event)
	}
}

func (rc *RemoteControl) processInput(event InputEvent) {
	switch event.Type {
	case "mousemove":
		rc.sendInputCommand("move", event.X, event.Y)
	case "click":
		rc.sendInputCommand("click", event.X, event.Y)
	case "wheel":
		rc.sendInputCommand("wheel", event.DeltaX, event.DeltaY)
	case "keydown", "keyup", "keycombo":
		rc.sendKeyCommand(event.Type, event.Key)
	}
}

func (rc *RemoteControl) sendInputCommand(cmdType string, x, y int) {
	if runtime.GOOS != "windows" {
		return
	}
	
	var command string
	switch cmdType {
	case "move":
		command = fmt.Sprintf("[System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(%d, %d)", x, y)
	case "click":
		command = fmt.Sprintf("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(%d, %d); [System.Windows.Forms.MouseEventSender]::SendClick()", x, y)
	case "wheel":
		command = fmt.Sprintf("Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Mouse]::MouseWheel(%d)", y)
	}
	
	exec.Command("powershell", "-Command", command).Run()
}

func (rc *RemoteControl) sendKeyCommand(cmdType, key string) {
	if runtime.GOOS != "windows" {
		return
	}
	
	key = strings.ToLower(key)
	
	var command string
	switch cmdType {
	case "keydown":
		command = fmt.Sprintf(`Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait("{%s}")`, key)
	case "keyup":
		command = ""
	case "keycombo":
		keys := strings.Split(key, "+")
		sendStr := ""
		for _, k := range keys {
			sendStr += fmt.Sprintf("{%s}", strings.ToLower(k))
		}
		command = fmt.Sprintf(`Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.SendKeys]::SendWait("%s")`, sendStr)
	}
	
	if command != "" {
		exec.Command("powershell", "-Command", command).Run()
	}
}

func (rc *RemoteControl) handleScreen(w http.ResponseWriter, r *http.Request) {
	if runtime.GOOS != "windows" {
		img := image.NewRGBA(image.Rect(0, 0, 800, 600))
		w.Header().Set("Content-Type", "image/jpeg")
		jpeg.Encode(w, img, &jpeg.Options{Quality: 75})
		return
	}

	cmd := exec.Command("powershell", "-Command", `
		Add-Type -AssemblyName System.Windows.Forms
		$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
		$bmp = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
		$graphics = [System.Drawing.Graphics]::FromImage($bmp)
		$graphics.CopyFromScreen($screen.Location, [System.Drawing.Point]::Empty, $screen.Size)
		$bmp.Save([Console]::OpenStandardOutput(), [System.Drawing.Imaging.ImageFormat]::Jpeg)
		$graphics.Dispose()
		$bmp.Dispose()
	`)
	
	output, err := cmd.Output()
	if err != nil {
		img := image.NewRGBA(image.Rect(0, 0, 800, 600))
		w.Header().Set("Content-Type", "image/jpeg")
		jpeg.Encode(w, img, &jpeg.Options{Quality: 75})
		return
	}

	w.Header().Set("Content-Type", "image/jpeg")
	w.Write(output)
}

func (rc *RemoteControl) handleInput(w http.ResponseWriter, r *http.Request) {
	if r.Method != "POST" {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var event InputEvent
	if err := json.NewDecoder(r.Body).Decode(&event); err != nil {
		http.Error(w, err.Error(), http.StatusBadRequest)
		return
	}

	rc.processInput(event)
	w.WriteHeader(http.StatusOK)
}

func (rc *RemoteControl) BroadcastScreen() {
	ticker := NewScreenCaptureTicker(100)
	for range ticker.C {
		var img image.Image
		if runtime.GOOS == "windows" {
			cmd := exec.Command("powershell", "-Command", `
				Add-Type -AssemblyName System.Windows.Forms
				$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
				$bmp = New-Object System.Drawing.Bitmap($screen.Width, $screen.Height)
				$graphics = [System.Drawing.Graphics]::FromImage($bmp)
				$graphics.CopyFromScreen($screen.Location, [System.Drawing.Point]::Empty, $screen.Size)
				$mem = New-Object System.IO.MemoryStream
				$bmp.Save($mem, [System.Drawing.Imaging.ImageFormat]::Jpeg)
				[Convert]::ToBase64String($mem.ToArray())
				$graphics.Dispose()
				$bmp.Dispose()
			`)
			
			_, err := cmd.Output()
			if err != nil {
				continue
			}
			
			img = image.NewRGBA(image.Rect(0, 0, 800, 600))
		} else {
			img = image.NewRGBA(image.Rect(0, 0, 800, 600))
		}

		var buf strings.Builder
		encoder := base64.NewEncoder(base64.StdEncoding, &buf)
		jpeg.Encode(encoder, img, &jpeg.Options{Quality: 60})
		encoder.Close()

		frame := buf.String()

		rc.mu.Lock()
		for _, client := range rc.clients {
			websocket.Message.Send(client, frame)
		}
		rc.mu.Unlock()
	}
}
