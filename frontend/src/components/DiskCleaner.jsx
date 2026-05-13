import { useState, useEffect } from 'react'

export default function DiskCleaner() {
  const [disks, setDisks] = useState([])
  const [cleanableFiles, setCleanableFiles] = useState([])
  const [totalSize, setTotalSize] = useState(0)
  const [software, setSoftware] = useState([])
  const [scanning, setScanning] = useState(false)
  const [cleaning, setCleaning] = useState(false)
  const [selectedFiles, setSelectedFiles] = useState([])
  const [cleanResult, setCleanResult] = useState(null)
  const [softwareSearch, setSoftwareSearch] = useState('')
  const [uninstalling, setUninstalling] = useState(null)
  const [showUninstallConfirm, setShowUninstallConfirm] = useState(null)

  useEffect(() => {
    loadDiskInfo()
    loadSoftware()
  }, [])

  const loadDiskInfo = async () => {
    try {
      const res = await fetch('/api/disk/info')
      const data = await res.json()
      setDisks(data.disks || [])
    } catch (err) {
      console.error('Failed to load disk info:', err)
    }
  }

  const loadSoftware = async () => {
    try {
      const res = await fetch('/api/disk/software')
      const data = await res.json()
      setSoftware(data.software || [])
    } catch (err) {
      console.error('Failed to load software:', err)
    }
  }

  const handleScan = async () => {
    setScanning(true)
    setCleanResult(null)
    try {
      const res = await fetch('/api/disk/scan')
      const data = await res.json()
      setCleanableFiles(data.files || [])
      setTotalSize(data.total_size || 0)
      setSelectedFiles(data.files?.map(f => f.path) || [])
    } catch (err) {
      console.error('Scan failed:', err)
    }
    setScanning(false)
  }

  const handleClean = async () => {
    if (selectedFiles.length === 0) return
    setCleaning(true)
    try {
      const res = await fetch('/api/disk/clean', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ files: selectedFiles })
      })
      const data = await res.json()
      setCleanResult(data)
      loadDiskInfo()
    } catch (err) {
      console.error('Clean failed:', err)
    }
    setCleaning(false)
  }

  const handleUninstall = async (name) => {
    setShowUninstallConfirm(name)
  }

  const confirmUninstall = async () => {
    if (!showUninstallConfirm) return
    setUninstalling(showUninstallConfirm)
    try {
      await fetch('/api/disk/uninstall', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: showUninstallConfirm })
      })
      loadSoftware()
    } catch (err) {
      console.error('Uninstall failed:', err)
    }
    setUninstalling(null)
    setShowUninstallConfirm(null)
  }

  const toggleFileSelection = (path) => {
    setSelectedFiles(prev =>
      prev.includes(path)
        ? prev.filter(p => p !== path)
        : [...prev, path]
    )
  }

  const selectAllFiles = () => {
    if (selectedFiles.length === cleanableFiles.length) {
      setSelectedFiles([])
    } else {
      setSelectedFiles(cleanableFiles.map(f => f.path))
    }
  }

  const formatSize = (bytes) => {
    if (bytes === 0) return '0 B'
    const k = 1024
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
    const i = Math.floor(Math.log(bytes) / Math.log(k))
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
  }

  const formatPercent = (percent) => {
    return percent.toFixed(1) + '%'
  }

  const getDiskColor = (percent) => {
    if (percent < 60) return '#22c55e'
    if (percent < 85) return '#f59e0b'
    return '#ef4444'
  }

  const filteredSoftware = software.filter(sw =>
    sw.name?.toLowerCase().includes(softwareSearch.toLowerCase()) ||
    sw.publisher?.toLowerCase().includes(softwareSearch.toLowerCase())
  )

  const filesByCategory = cleanableFiles.reduce((acc, file) => {
    if (!acc[file.category]) {
      acc[file.category] = { files: [], totalSize: 0 }
    }
    acc[file.category].files.push(file)
    acc[file.category].totalSize += file.size
    return acc
  }, {})

  return (
    <div className="disk-cleaner">
      <div className="disk-cleaner-header">
        <h2>Disk Cleaner</h2>
        <p className="hint">Manage disk space, clean temporary files, and uninstall software</p>
      </div>

      <div className="disk-info-section">
        <h3>Disk Usage</h3>
        <div className="disk-cards">
          {disks.map((disk, idx) => (
            <div key={idx} className="disk-card">
              <div className="disk-card-header">
                <span className="disk-letter">{disk.path.charAt(0)}</span>
                <span className="disk-path">{disk.path}</span>
              </div>
              <div className="disk-progress">
                <div
                  className="disk-progress-bar"
                  style={{
                    width: formatPercent(disk.percent),
                    backgroundColor: getDiskColor(disk.percent)
                  }}
                />
              </div>
              <div className="disk-stats">
                <span>Used: {formatSize(disk.used)}</span>
                <span>Free: {formatSize(disk.free)}</span>
                <span>Total: {formatSize(disk.total)}</span>
                <span className="disk-percent" style={{ color: getDiskColor(disk.percent) }}>
                  {formatPercent(disk.percent)}
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="cleanable-section">
        <div className="section-header">
          <h3>Cleanable Files</h3>
          <button
            className="btn-scan"
            onClick={handleScan}
            disabled={scanning}
          >
            {scanning ? 'Scanning...' : 'Scan Files'}
          </button>
        </div>

        {cleanableFiles.length > 0 && (
          <div className="cleanable-content">
            <div className="clean-summary">
              <span>Found {cleanableFiles.length} files, total {formatSize(totalSize)}</span>
              <button className="btn-select-all" onClick={selectAllFiles}>
                {selectedFiles.length === cleanableFiles.length ? 'Deselect All' : 'Select All'}
              </button>
            </div>

            {Object.entries(filesByCategory).map(([category, data]) => (
              <div key={category} className="category-group">
                <div className="category-header">
                  <span>{category}</span>
                  <span>{data.files.length} files, {formatSize(data.totalSize)}</span>
                </div>
                <div className="category-files">
                  {data.files.map((file, idx) => (
                    <label key={idx} className="file-item">
                      <input
                        type="checkbox"
                        checked={selectedFiles.includes(file.path)}
                        onChange={() => toggleFileSelection(file.path)}
                      />
                      <span className="file-name">{file.path}</span>
                      <span className="file-size">{formatSize(file.size)}</span>
                    </label>
                  ))}
                </div>
              </div>
            ))}

            <div className="clean-actions">
              <button
                className="btn-clean"
                onClick={handleClean}
                disabled={cleaning || selectedFiles.length === 0}
              >
                {cleaning ? 'Cleaning...' : `Clean Selected (${selectedFiles.length} files)`}
              </button>
            </div>

            {cleanResult && (
              <div className="clean-result">
                <h4>Clean Result</h4>
                <p>Scanned: {cleanResult.scanned_files} files</p>
                <p>Deleted: {cleanResult.deleted_files} files</p>
                <p>Freed: {formatSize(cleanResult.freed_space)}</p>
                {cleanResult.errors?.length > 0 && (
                  <div className="clean-errors">
                    <p>Errors ({cleanResult.errors.length}):</p>
                    <ul>
                      {cleanResult.errors.slice(0, 5).map((err, idx) => (
                        <li key={idx}>{err}</li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>

      <div className="software-section">
        <div className="section-header">
          <h3>Installed Software</h3>
          <input
            type="text"
            className="software-search"
            placeholder="Search software..."
            value={softwareSearch}
            onChange={(e) => setSoftwareSearch(e.target.value)}
          />
        </div>

        <div className="software-list">
          {filteredSoftware.slice(0, 50).map((sw, idx) => (
            <div key={idx} className="software-item">
              <div className="software-info">
                <span className="software-name">{sw.name}</span>
                <span className="software-version">v{sw.version || 'N/A'}</span>
                <span className="software-publisher">{sw.publisher || 'Unknown'}</span>
                <span className="software-size">{formatSize(sw.size)}</span>
              </div>
              <button
                className="btn-uninstall"
                onClick={() => handleUninstall(sw.name)}
                disabled={uninstalling === sw.name}
              >
                {uninstalling === sw.name ? 'Uninstalling...' : 'Uninstall'}
              </button>
            </div>
          ))}
          {filteredSoftware.length > 50 && (
            <div className="software-more">
              Showing 50 of {filteredSoftware.length} results. Use search to find specific software.
            </div>
          )}
        </div>
      </div>

      {showUninstallConfirm && (
        <div className="modal-overlay" onClick={() => setShowUninstallConfirm(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h3>Confirm Uninstall</h3>
            <p>Are you sure you want to forcefully uninstall "{showUninstallConfirm}"?</p>
            <p className="warning-text">This will remove the software and its related files. This action cannot be undone.</p>
            <div className="modal-actions">
              <button className="btn-cancel" onClick={() => setShowUninstallConfirm(null)}>
                Cancel
              </button>
              <button className="btn-confirm-uninstall" onClick={confirmUninstall}>
                Uninstall
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
