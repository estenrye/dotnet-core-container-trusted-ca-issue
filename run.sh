docker build -t thycotic-test .
docker run --rm -it \
  -e "Thycotic__Uri=${Thycotic__Uri}" \
  -e "Thycotic__RuleName=${Thycotic__RuleName}" \
  -e "Thycotic__RuleKey=${Thycotic__RuleKey}" \
  -e "Thycotic__SearchTemplateName=${Thycotic__SearchTemplateName}" \
  -e "Thycotic__SearchText=${Thycotic__SearchText}" \
  -e "Thycotic__Username=${THYCOTIC_USERNAME}" \
  -e "Thycotic__Password=${THYCOTIC_PASSWORD}" \
  -e "Thycotic__GrantType=${THYCOTIC_GRANT_TYPE}" \
  thycotic-test