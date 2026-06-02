import { useEffect } from 'react'
import { Grid, Typography } from '@mui/material'
import EventsBar from '@/components/debug/EventsBar'
import SessionList from '@/components/debug/SessionList'
import QueryRunner from '@/components/debug/QueryRunner'
import QueryResults from '@/components/debug/QueryResults'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'

export default function DebugPage() {
  const fetchSessions = useDiagnosticsStore((s) => s.fetchSessions)

  useEffect(() => {
    fetchSessions()
  }, [fetchSessions])

  return (
    <>
      <Typography variant="h5" sx={{ mb: 2 }}>
        Debug Playground
      </Typography>

      <EventsBar />

      <Grid container spacing={3}>
        {/* Left panel */}
        <Grid size={{ xs: 12, md: 5 }}>
          <SessionList />
          <QueryRunner />
        </Grid>

        {/* Right panel */}
        <Grid size={{ xs: 12, md: 7 }}>
          <QueryResults />
        </Grid>
      </Grid>
    </>
  )
}
