import { ListItemButton, ListItemText, Typography } from '@mui/material'
import type { ProcessInfo } from '@/types/api'

interface ProcessItemProps {
  process: ProcessInfo
  selected: boolean
  onSelect: (p: ProcessInfo) => void
}

export default function ProcessItem({ process, selected, onSelect }: ProcessItemProps) {
  return (
    <ListItemButton selected={selected} onClick={() => onSelect(process)} sx={{ borderRadius: 1, mb: 0.5 }}>
      <ListItemText
        primary={
          <Typography variant="body1" sx={{ fontWeight: selected ? 600 : 400 }}>
            {process.name}
          </Typography>
        }
        secondary={`PID: ${process.id}`}
      />
    </ListItemButton>
  )
}
