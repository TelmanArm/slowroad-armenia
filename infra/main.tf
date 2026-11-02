data "aws_subnet" "default" {
  availability_zone = var.availability_zone
  default_for_az    = true
}

module "ec2_instance" {
  source  = "terraform-aws-modules/ec2-instance/aws"
  version = "6.4.1"

  name          = var.instance_name
  instance_type = var.instance_type
  key_name      = var.key_name
  subnet_id     = data.aws_subnet.default.id

  security_group_ingress_rules = {
    ssh   = { from_port = 22, to_port = 22, cidr_ipv4 = var.ssh_allowed_cidr }
    http  = { from_port = 80, to_port = 80, cidr_ipv4 = "0.0.0.0/0" }
    https = { from_port = 443, to_port = 443, cidr_ipv4 = "0.0.0.0/0" }
  }

  tags = {
    Project = var.project
    Env     = var.environment
  }
}

output "public_ip" {
  value = module.ec2_instance.public_ip
}
