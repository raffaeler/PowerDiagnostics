import { ListItemButton, Typography, Box } from '@mui/material'
import type { ProcessInfo } from '@/types/api'

interface ProcessItemProps {
  process: ProcessInfo
  selected: boolean
  onSelect: (p: ProcessInfo) => void
}

export default function ProcessItem({ process, selected, onSelect }: ProcessItemProps) {
  return (
    <ListItemButton
      selected={selected}
      onClick={() => onSelect(process)}
      sx={{
        borderRadius: 1,
        mb: 0.25,
        py: 0.5,
        minHeight: 32,
      }}
    >
      <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', width: '100%' }}>
        <Typography
          variant="body2"
          sx={{
            fontFamily: 'monospace',
            fontWeight: selected ? 700 : 400,
            color: 'text.secondary',
            minWidth: 60,
          }}
        >
          {process.id}
        </Typography>
        <Typography
          variant="body2"
          sx={{ fontWeight: selected ? 600 : 400 }}
        >
          {process.name}
        </Typography>
      </Box>
    </ListItemButton>
  )
}
