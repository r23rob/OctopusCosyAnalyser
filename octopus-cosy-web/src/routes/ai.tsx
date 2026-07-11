import { createFileRoute } from '@tanstack/react-router'
import { AiAnalysisPage } from '@/components/ai/AiAnalysisPage'

export const Route = createFileRoute('/ai')({
  component: AiAnalysisPage,
})
