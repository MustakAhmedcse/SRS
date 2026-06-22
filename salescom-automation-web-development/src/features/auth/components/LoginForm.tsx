"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { LoginInputSchema, type LoginInput } from "../schema";
import { useLogin } from "../hooks";

export function LoginForm() {
  const form = useForm<LoginInput>({
    resolver: zodResolver(LoginInputSchema),
    defaultValues: { username: "", password: "", rememberMe: false },
  });
  const login = useLogin();

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit((input) => login.mutate(input))}
        className="space-y-4"
      >
        <FormField
          control={form.control}
          name="username"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Username</FormLabel>
              <FormControl>
                <Input autoComplete="username" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="password"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Password</FormLabel>
              <FormControl>
                <Input
                  type="password"
                  autoComplete="current-password"
                  {...field}
                />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="rememberMe"
          render={({ field }) => (
            <FormItem className="flex items-center justify-between">
              <FormLabel htmlFor="rememberMe">Remember me</FormLabel>
              <FormControl>
                <Switch
                  id="rememberMe"
                  checked={field.value ?? false}
                  onCheckedChange={field.onChange}
                  disabled={field.disabled}
                />
              </FormControl>
            </FormItem>
          )}
        />
        <Button type="submit" className="w-full" disabled={login.isPending}>
          {login.isPending ? "Signing in…" : "Sign in"}
        </Button>
      </form>
    </Form>
  );
}
