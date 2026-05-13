import { useState, useEffect } from 'react'
import Welcome from './pages/Welcome'
import SetupOllama from './pages/SetupOllama'
import SetupModel from './pages/SetupModel'
import SetupConfigure from './pages/SetupConfigure'
import Dashboard from './pages/Dashboard'

function App() {
  const [step, setStep] = useState(0)
  const [ollamaInstalled, setOllamaInstalled] = useState(false)
  const [ollamaRunning, setOllamaRunning] = useState(false)
  const [setupComplete, setSetupComplete] = useState(false)

  useEffect(() => {
    fetch('/api/setup/check')
      .then(r => r.json())
      .then(data => {
        if (data.setup_complete) {
          setStep(4)
          return
        }
        setOllamaInstalled(data.ollama_installed)
        setOllamaRunning(data.ollama_running)
        setStep(1)
      })
  }, [])

  const handleNext = (nextStep) => {
    setStep(nextStep)
  }

  switch (step) {
    case 0:
      return <Welcome onNext={() => handleNext(1)} />
    case 1:
      return <SetupOllama 
        installed={ollamaInstalled} 
        running={ollamaRunning}
        onNext={() => handleNext(2)} 
      />
    case 2:
      return <SetupModel onNext={() => handleNext(3)} />
    case 3:
      return <SetupConfigure onComplete={() => {
        setSetupComplete(true)
        handleNext(4)
      }} />
    case 4:
      return <Dashboard />
    default:
      return <Welcome onNext={() => handleNext(1)} />
  }
}

export default App
