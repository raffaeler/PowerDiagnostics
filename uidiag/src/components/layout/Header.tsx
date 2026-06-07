import { useState } from 'react'
import {
  AppBar,
  Toolbar,
  Typography,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Box,
  IconButton,
  useTheme,
} from '@mui/material'
import DarkModeIcon from '@mui/icons-material/DarkMode'
import LightModeIcon from '@mui/icons-material/LightMode'
import CloseIcon from '@mui/icons-material/Close'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAppStore } from '@/stores/useAppStore'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'

export default function Header() {
  const navigate = useNavigate()
  const location = useLocation()
  const theme = useTheme()
  const { username, isLoggedIn, login, logout, darkMode, toggleDarkMode } = useAppStore()
  const activeSessionId = useDiagnosticsStore((s) => s.activeSessionId)
  const closeSession = useDiagnosticsStore((s) => s.closeSession)

  const [loginOpen, setLoginOpen] = useState(false)
  const [nameInput, setNameInput] = useState('')

  const handleLogin = () => {
    if (nameInput.trim()) {
      login(nameInput.trim())
      setLoginOpen(false)
      setNameInput('')
    }
  }

  const handleLogout = () => {
    logout()
  }

  const hasActiveSession = !!activeSessionId

  const handleCloseSession = async () => {
    if (activeSessionId) {
      await closeSession(activeSessionId)
      navigate('/')
    }
  }

  const navItems = [
    { label: 'Home', path: '/', disabled: hasActiveSession },
    { label: 'Debug', path: activeSessionId ? `/debug/${activeSessionId}` : '/debug' },
  ]

  return (
    <>
      <AppBar position="sticky" elevation={2}>
        <Toolbar>
          {/* Left: Title */}
          <Typography variant="h6" sx={{ mr: 4, fontWeight: 700 }}>
            PowerDiagnostics
          </Typography>

          {/* Center: Navigation */}
          <Box sx={{ display: 'flex', gap: 1, flexGrow: 1 }}>
            {navItems.map((item) => (
              <Button
                key={item.path}
                color="inherit"
                onClick={() => navigate(item.path)}
                disabled={item.disabled}
                sx={{
                  textTransform: 'none',
                  fontWeight:
                    location.pathname === item.path || location.pathname.startsWith(item.path + '/')
                      ? 700
                      : 400,
                  borderBottom:
                    location.pathname === item.path || location.pathname.startsWith(item.path + '/')
                      ? `2px solid ${theme.palette.primary.contrastText}`
                      : '2px solid transparent',
                  borderRadius: 0,
                }}
              >
                {item.label}
              </Button>
            ))}
          </Box>

          {/* Right: Close Session + Theme toggle + Username / Login */}
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            {hasActiveSession && (
              <Button
                color="error"
                variant="outlined"
                size="small"
                startIcon={<CloseIcon />}
                onClick={handleCloseSession}
                sx={{ textTransform: 'none', borderColor: 'rgba(255,255,255,0.5)', color: 'inherit' }}
              >
                Close Session
              </Button>
            )}
            <IconButton color="inherit" onClick={toggleDarkMode} size="small" aria-label="toggle dark mode">
              {darkMode ? <LightModeIcon /> : <DarkModeIcon />}
            </IconButton>
            {isLoggedIn ? (
              <>
                <Typography variant="body2" sx={{ opacity: 0.9 }}>
                  {username}
                </Typography>
                <Button color="inherit" size="small" onClick={handleLogout} sx={{ textTransform: 'none' }}>
                  Logout
                </Button>
              </>
            ) : (
              <Button color="inherit" size="small" onClick={() => setLoginOpen(true)} sx={{ textTransform: 'none' }}>
                Login
              </Button>
            )}
          </Box>
        </Toolbar>
      </AppBar>

      {/* Login Dialog */}
      <Dialog open={loginOpen} onClose={() => setLoginOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Enter your name</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            label="Username"
            variant="outlined"
            value={nameInput}
            onChange={(e) => setNameInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') handleLogin()
            }}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setLoginOpen(false)}>Cancel</Button>
          <Button variant="contained" onClick={handleLogin} disabled={!nameInput.trim()}>
            Login
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
