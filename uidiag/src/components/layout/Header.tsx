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
} from '@mui/material'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAppStore } from '@/stores/useAppStore'

export default function Header() {
  const navigate = useNavigate()
  const location = useLocation()
  const { username, isLoggedIn, login, logout } = useAppStore()

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

  const navItems = [
    { label: 'Home', path: '/' },
    { label: 'Debug', path: '/debug' },
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
                sx={{
                  textTransform: 'none',
                  fontWeight: location.pathname === item.path ? 700 : 400,
                  borderBottom: location.pathname === item.path ? '2px solid white' : '2px solid transparent',
                  borderRadius: 0,
                }}
              >
                {item.label}
              </Button>
            ))}
          </Box>

          {/* Right: Username / Login */}
          {isLoggedIn ? (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Typography variant="body2" sx={{ opacity: 0.9 }}>
                {username}
              </Typography>
              <Button color="inherit" size="small" onClick={handleLogout} sx={{ textTransform: 'none' }}>
                Logout
              </Button>
            </Box>
          ) : (
            <Button color="inherit" size="small" onClick={() => setLoginOpen(true)} sx={{ textTransform: 'none' }}>
              Login
            </Button>
          )}
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
