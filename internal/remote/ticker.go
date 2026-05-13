package remote

import (
	"time"
)

type ScreenCaptureTicker struct {
	C chan struct{}
}

func NewScreenCaptureTicker(intervalMs int) *ScreenCaptureTicker {
	ticker := time.NewTicker(time.Duration(intervalMs) * time.Millisecond)
	c := make(chan struct{}, 1)

	go func() {
		for range ticker.C {
			select {
			case c <- struct{}{}:
			default:
			}
		}
	}()

	return &ScreenCaptureTicker{C: c}
}
