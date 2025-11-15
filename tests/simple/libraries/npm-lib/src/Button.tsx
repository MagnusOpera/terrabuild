import type { ButtonHTMLAttributes, ReactNode } from 'react'

type ButtonProps = {
  children: ReactNode
} & ButtonHTMLAttributes<HTMLButtonElement>

export function Button(props: ButtonProps) {
  const { children, ...rest } = props

  return (
    <button {...rest}>
      {children}
    </button>
  )
}