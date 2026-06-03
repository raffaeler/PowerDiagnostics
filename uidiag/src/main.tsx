import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@/utils/debug' // Initialize window.__uidiag_debug before React renders
import App from '@/App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
