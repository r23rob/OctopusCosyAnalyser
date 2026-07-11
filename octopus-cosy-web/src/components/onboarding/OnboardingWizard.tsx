import { useState } from 'react'
import { WelcomeStep } from './WelcomeStep'
import { ConnectStep } from './ConnectStep'
import { DiscoverStep } from './DiscoverStep'
import { DoneStep } from './DoneStep'

type Step = 'welcome' | 'connect' | 'discover' | 'done'

export function OnboardingWizard() {
  const [step, setStep] = useState<Step>('welcome')
  const [accountNumber, setAccountNumber] = useState('')

  return (
    <div className="fixed inset-0 z-[999]">
      {step === 'welcome' && (
        <WelcomeStep onNext={() => setStep('connect')} />
      )}

      {step === 'connect' && (
        <ConnectStep
          onBack={() => setStep('welcome')}
          onNext={(acct) => {
            setAccountNumber(acct)
            setStep('discover')
          }}
        />
      )}

      {step === 'discover' && (
        <DiscoverStep
          accountNumber={accountNumber}
          onNext={() => setStep('done')}
        />
      )}

      {step === 'done' && <DoneStep />}
    </div>
  )
}
