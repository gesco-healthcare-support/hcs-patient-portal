
export interface ResetPasswordInput {
  userId: string;
  resetToken: string;
  password: string;
  confirmPassword: string;
}

export interface SendPasswordResetCodeInput {
  email: string;
  returnUrl?: string | null;
}
