import { useEffect } from 'react'
import { Outlet } from 'react-router-dom'
import { Box } from '@mui/material'
import Header from './Header'
import Footer from './Footer'
import ToastProvider from '@/components/shared/ToastProvider'
import { useSignalRStore } from '@/stores/useSignalRStore'

/**
 * Root layout shell: persistent header + footer with page content via <Outlet />.
 * Initializes the SignalR connection on mount.
 */
export default function AppLayout() {
  const connect = useSignalRStore((s) => s.connect)

  useEffect(() => {
    connect()
  }, [connect])

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <Header />
      <Box component="main" sx={{ flexGrow: 1, p: 3 }}>
        <Outlet />
      </Box>
      <Footer />
      <ToastProvider />
    </Box>
  )
}
