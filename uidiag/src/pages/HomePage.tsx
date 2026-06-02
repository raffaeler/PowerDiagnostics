import { Grid, Typography } from '@mui/material'
import ProcessPicker from '@/components/home/ProcessPicker'
import SessionActions from '@/components/home/SessionActions'

export default function HomePage() {
  return (
    <>
      <Typography variant="h5" sx={{ mb: 3 }}>
        Home
      </Typography>

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 7 }}>
          <ProcessPicker />
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <SessionActions />
        </Grid>
      </Grid>
    </>
  )
}
