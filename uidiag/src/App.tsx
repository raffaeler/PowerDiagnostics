import { CssBaseline, ThemeProvider } from '@mui/material'
import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { theme } from '@/theme'
import AppLayout from '@/components/layout/AppLayout'
import HomePage from '@/pages/HomePage'
import DebugPage from '@/pages/DebugPage'

function App() {
  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <BrowserRouter>
        <Routes>
          <Route element={<AppLayout />}>
            <Route index element={<HomePage />} />
            <Route path="debug" element={<DebugPage />} />
            <Route path="debug/:sessionId" element={<DebugPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </ThemeProvider>
  )
}

export default App
